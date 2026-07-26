using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MessageHook.Core.Messaging.Models;
using MessageHook.Core.Messaging.Publishing.Entities;
using MessageHook.Orchestration.Configurations;
using MessageHook.Orchestration.Entities.Enums;
using MessageHook.Orchestration.Entities.Interfaces;
using MessageHook.Orchestration.Factories;
using MessageHook.Playbook.Hosting;
using MessageHook.Playbook.Loading;
using MessageHook.Playbook.Models;
using MessageHook.Playbook.Results;
using MessageHook.Playbook.Serialization;
using MessageHook.Playbook.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHook.Playbook.Execution;

/// <summary>Executes one step: resolve payload, produce/consume via the existing MessageHook factory, validate.</summary>
public sealed class StepExecutor
{
    private readonly PlaybookHost _host;
    private readonly PayloadResolver _payloadResolver;
    private readonly PlaceholderExpander _expander;
    private readonly ValidationRuleRegistry _rules;
    private readonly SerializerRegistry _serializers;
    private readonly PlaybookDefinition _definition;
    private readonly IPayloadProvider _payloadProvider;

    public StepExecutor(
        PlaybookHost host,
        PayloadResolver payloadResolver,
        PlaceholderExpander expander,
        ValidationRuleRegistry rules,
        SerializerRegistry serializers,
        PlaybookDefinition definition,
        IPayloadProvider payloadProvider)
    {
        _host = host;
        _payloadResolver = payloadResolver;
        _expander = expander;
        _rules = rules;
        _serializers = serializers;
        _definition = definition;
        _payloadProvider = payloadProvider;
    }

    public async Task<StepResult> ExecuteAsync(PlaybookStep step, PlaybookContext context, CancellationToken ct)
    {
        var result = new StepResult { Name = step.Name };

        try
        {
            var effectiveOverrides = MergeOverrides(_definition.Override, step.Override);
            var payload = _payloadResolver.Resolve(step.Send, effectiveOverrides, context, _payloadProvider);

            var key = _expander.ExpandString(step.Key ?? string.Empty, context.Variables);
            var extraData = new ProducingExtraData();
            foreach (var header in step.Headers ?? new Dictionary<string, string>())
                extraData.Headers[header.Key] = _expander.ExpandString(header.Value, context.Variables);

            var mode = step.Match?.Mode ?? MatchMode.CorrelationId;

            // Same rule the engine applies in BaseMessageHookStep: the topics decide the shape. ProduceAndForget
            // never waits, so its expected count is 0 whatever the playbook wrote.
            var hookType = step.HookType;
            var hasProduce = hookType != MessageHookType.ConsumeOnly;
            var expectedCount = step.EffectiveMessageCount;

            var expectedKey = mode == MatchMode.MessageKey
                ? (string.IsNullOrWhiteSpace(step.Match?.ExpectedKey)
                    ? key
                    : _expander.ExpandString(step.Match!.ExpectedKey!, context.Variables))
                : null;

            var configuration = new MessageHookConfiguration
            {
                ProduceTo = step.ProduceTo,
                ConsumeFrom = step.ConsumeFrom ?? new List<string>(),
                ConsumingOptions = new ConsumingOptionsConfiguration
                {
                    TimeOut = TimeSpan.FromSeconds(step.TimeoutSeconds),
                    MsgReceivedCount = expectedCount,
                    ExpectedMessageKey = expectedKey
                }
            };

            var factory = _host.Services.GetRequiredService<IMessageHookFactory>();
            IMessageHookStep hookStep = await factory.CreateMessageHookStepAsync(configuration);

            TaskCompletionSource<IEnumerable<ResponseContainer>> completion;
            if (hasProduce)
            {
                var produceObject = BuildProduceObject(payload, step.ProduceTo!);
                completion = await hookStep.ExecuteAsync(key, produceObject, extraData);
            }
            else
            {
                completion = await hookStep.ExecuteAsync();
            }

            IEnumerable<ResponseContainer> containers;
            try
            {
                containers = await completion.Task.WaitAsync(ct);
            }
            catch (TimeoutException)
            {
                result.Error = $"Timed out after {step.TimeoutSeconds}s waiting for {expectedCount} " +
                               $"message(s) on [{string.Join(", ", step.ConsumeFrom ?? new List<string>())}].";
                result.Passed = false;
                return result;
            }

            var messages = containers.SelectMany(c => c.Messages).ToList();
            result.ReceivedMessageCount = messages.Count;

            var first = messages.Count > 0 ? messages[0] : null;
            RunCaptures(step, first, context);
            RunValidations(step, first, result);

            result.Passed = result.Error is null
                            && result.ReceivedMessageCount >= expectedCount
                            && result.Validations.All(v => v.Passed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.Passed = false;
        }

        return result;
    }

    private static Dictionary<string, JsonNode?> MergeOverrides(
        IReadOnlyDictionary<string, JsonNode?>? fileLevel, IReadOnlyDictionary<string, JsonNode?>? stepLevel)
    {
        var merged = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var kv in fileLevel ?? new Dictionary<string, JsonNode?>()) merged[kv.Key] = kv.Value;
        foreach (var kv in stepLevel ?? new Dictionary<string, JsonNode?>()) merged[kv.Key] = kv.Value;
        return merged;
    }

    private object BuildProduceObject(JsonNode? payload, string produceTopic)
    {
        var json = payload?.ToJsonString() ?? "{}";
        var declaration = _definition.ProduceTopics.FirstOrDefault(t => t.Topic == produceTopic);

        if (!string.IsNullOrWhiteSpace(declaration?.MessageType))
        {
            var type = _serializers.ResolveType(declaration!.MessageType);
            return JsonSerializer.Deserialize(json, type)
                   ?? throw new PlaybookException($"Payload for '{produceTopic}' deserialized to null for type {type}.");
        }

        // Schema-less: hand the producer a Utf8Json-native dictionary so KafkaUTF8Serializer re-serializes it cleanly.
        return Utf8Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
               ?? new Dictionary<string, object>();
    }

    private void RunCaptures(PlaybookStep step, MessageContainer? message, PlaybookContext context)
    {
        if (step.Capture is null || message is null) return;
        foreach (var (name, path) in step.Capture)
            if (MessagePathResolver.TryResolve(message.Value, path, out var value))
                context.SetVariable(name, MessagePathResolver.Format(value) ?? string.Empty);
    }

    private void RunValidations(PlaybookStep step, MessageContainer? message, StepResult result)
    {
        foreach (var validation in step.Validations ?? Enumerable.Empty<ValidationDefinition>())
        {
            bool found;
            object? actual;

            if (validation.Target == ValidationTarget.Key)
            {
                var keyString = message is null ? null : DecodeKey(message.Key);
                found = keyString is not null && MessagePath.IsRoot(validation.Path);
                actual = found ? keyString : null;
            }
            else if (message is null)
            {
                found = false;
                actual = null;
            }
            else
            {
                found = MessagePathResolver.TryResolve(message.Value, validation.Path, out actual);
                if (!found) actual = null;
            }

            var rule = _rules.Get(validation.Type);
            var passed = rule.IsSatisfied(found, actual, validation.Expected);

            result.Validations.Add(new ValidationResult
            {
                Target = validation.Target.ToString(),
                Path = validation.Path,
                Type = validation.Type,
                Expected = validation.Expected?.ToJsonString().Trim('"'),
                Actual = MessagePathResolver.Format(actual),
                Passed = passed
            });
        }
    }

    private static string? DecodeKey(object? key) => key switch
    {
        null => null,
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        string s => s,
        _ => key.ToString()
    };
}
