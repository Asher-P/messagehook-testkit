using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MessageHook.Orchestration.Entities.Enums;
using MessageHook.Playbook.Models;
using MessageHook.Playbook.Serialization;
using MessageHook.Playbook.Validation;

namespace MessageHook.Playbook.Loading;

/// <summary>
/// Broker-free semantic checks. Runs before anything connects to Kafka, so a UI can validate as the user builds
/// and a service can reject a bad request fast. Reports every problem it finds, not just the first.
/// </summary>
public sealed class PlaybookValidator
{
    private static readonly Regex VarReference = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);
    private static readonly HashSet<string> ExpectedOptional = new(StringComparer.OrdinalIgnoreCase) { "Exists", "NotExists" };

    private readonly SerializerRegistry _serializers;
    private readonly ValidationRuleRegistry _rules;

    public PlaybookValidator(SerializerRegistry serializers, ValidationRuleRegistry rules)
    {
        _serializers = serializers;
        _rules = rules;
    }

    /// <summary>Throws <see cref="PlaybookException"/> carrying all errors when the playbook is invalid.</summary>
    public void Validate(PlaybookDefinition def, IPayloadProvider payloadProvider)
    {
        var errors = new List<string>();

        if (def.KafkaConfiguration.BootstrapServers.Count == 0)
            errors.Add("KafkaConfiguration.BootstrapServers must have at least one entry.");

        var consumeTopics = ValidateTopics(def.ConsumeTopics, isConsumer: true, errors);
        var produceTopics = ValidateTopics(def.ProduceTopics, isConsumer: false, errors);

        if (def.Test.Steps.Count == 0)
            errors.Add("Test.Steps must have at least one step.");

        for (var i = 0; i < def.Test.Steps.Count; i++)
            ValidateStep(def.Test.Steps[i], i, consumeTopics, produceTopics, def.ProduceTopics, payloadProvider, errors);

        if (def.StrictOverride)
            ValidateStrictOverride(def, payloadProvider, errors);

        if (errors.Count > 0)
            throw new PlaybookException(errors);
    }

    private Dictionary<string, TopicDeclaration> ValidateTopics(
        List<TopicDeclaration> topics, bool isConsumer, List<string> errors)
    {
        var map = new Dictionary<string, TopicDeclaration>(StringComparer.Ordinal);
        foreach (var topic in topics)
        {
            if (string.IsNullOrWhiteSpace(topic.Topic))
            {
                errors.Add($"A {(isConsumer ? "consume" : "produce")} topic declaration has an empty name.");
                continue;
            }

            map[topic.Topic] = topic;

            var isProtobuf = topic.Serializer.Trim().Equals("Protobuf", StringComparison.OrdinalIgnoreCase);
            if (isConsumer && isProtobuf)
                errors.Add($"Consume topic '{topic.Topic}' uses Protobuf, which has no deserializer; use Json.");
            if (!isConsumer && isProtobuf && string.IsNullOrWhiteSpace(topic.MessageType))
                errors.Add($"Produce topic '{topic.Topic}' uses Protobuf but has no 'messageType' (required for Protobuf).");

            if (!string.IsNullOrWhiteSpace(topic.MessageType))
            {
                try { _serializers.ResolveType(topic.MessageType); }
                catch (PlaybookException e) { errors.Add(e.Message); }
            }
        }

        return map;
    }

    private void ValidateStep(
        PlaybookStep step, int index,
        Dictionary<string, TopicDeclaration> consumeTopics,
        Dictionary<string, TopicDeclaration> produceTopics,
        List<TopicDeclaration> produceDeclarations,
        IPayloadProvider payloadProvider,
        List<string> errors)
    {
        var label = $"step[{index}]{(step.Name is null ? "" : $" '{step.Name}'")}";

        var hasProduce = !string.IsNullOrWhiteSpace(step.ProduceTo);
        if (hasProduce && !produceTopics.ContainsKey(step.ProduceTo!))
            errors.Add($"{label}: ProduceTo '{step.ProduceTo}' is not declared in ProduceTopics.");

        var hasConsume = step.ConsumeFrom is { Count: > 0 };
        foreach (var topic in step.ConsumeFrom ?? Enumerable.Empty<string>())
            if (!consumeTopics.ContainsKey(topic))
                errors.Add($"{label}: ConsumeFrom '{topic}' is not declared in ConsumeTopics.");

        // The topics decide the shape, exactly as the engine decides it in BaseMessageHookStep: no ConsumeFrom is
        // fire-and-forget (produce and move on without waiting), so nothing comes back to match, capture or validate.
        // ExpectedMessageCount is not what makes a step produce-only — it is simply ignored on that shape.
        var hookType = step.HookType;
        var produceOnly = hookType == MessageHookType.ProduceAndForget;

        if (!hasProduce && !hasConsume)
            errors.Add($"{label}: a step must produce (ProduceTo) or consume (ConsumeFrom); this one does neither.");

        if (produceOnly)
        {
            if (step.Validations is { Count: > 0 })
                errors.Add($"{label}: a produce-only step receives no message, so it cannot have Validations. " +
                           "Add a ConsumeFrom, or validate in a later step.");
            if (step.Capture is { Count: > 0 })
                errors.Add($"{label}: a produce-only step receives no message, so it cannot Capture from one.");
        }
        else if (step.EffectiveMessageCount < 1)
        {
            errors.Add($"{label}: ExpectedMessageCount must be at least 1 when ConsumeFrom is set " +
                       $"(0 would wait out the full {step.TimeoutSeconds}s timeout and fail).");
        }

        var mode = step.Match?.Mode ?? MatchMode.CorrelationId;
        if (hasConsume)
        {
            if (mode == MatchMode.CorrelationId && !hasProduce)
                errors.Add($"{label}: CorrelationId matching requires a ProduceTo (the correlation id is injected on produce). " +
                           "For a consume-only step use Match.mode 'MessageKey'.");
            if (mode == MatchMode.MessageKey && string.IsNullOrWhiteSpace(step.Match?.ExpectedKey) && !hasProduce)
                errors.Add($"{label}: MessageKey matching on a consume-only step requires Match.expectedKey.");
        }

        if (hasProduce && step.Send is null)
            errors.Add($"{label}: produces to '{step.ProduceTo}' but has no 'Send' payload.");
        if (!hasProduce && step.Send is not null)
            errors.Add($"{label}: has a 'Send' payload but no 'ProduceTo' — a consume-only step must not send.");

        if (step.Send is { IsFileReference: true } send && !payloadProvider.Exists(send.File!))
            errors.Add($"{label}: Send payload file '{send.File}' cannot be resolved.");

        // Protobuf produce needs a typed payload → messageType must be present on the produce topic.
        if (hasProduce && produceTopics.TryGetValue(step.ProduceTo!, out var pt)
            && pt.Serializer.Trim().Equals("Protobuf", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(pt.MessageType))
            errors.Add($"{label}: produces Protobuf to '{step.ProduceTo}' which needs a 'messageType'.");

        foreach (var v in step.Validations ?? Enumerable.Empty<ValidationDefinition>())
        {
            if (!_rules.IsKnown(v.Type))
                errors.Add($"{label}: unknown validation type '{v.Type}'. Known: {string.Join(", ", _rules.KnownTypes)}.");
            else if (v.Expected is null && !ExpectedOptional.Contains(v.Type))
                errors.Add($"{label}: validation '{v.Type}' on path '{v.Path}' requires an 'expected' value.");
        }
    }

    private void ValidateStrictOverride(PlaybookDefinition def, IPayloadProvider payloadProvider, List<string> errors)
    {
        var referenced = GatherReferencedTokens(def, payloadProvider);
        var payloadPaths = GatherPayloadPaths(def, payloadProvider);

        void Check(string scope, IEnumerable<string>? keys)
        {
            foreach (var key in keys ?? Enumerable.Empty<string>())
                if (!payloadPaths.Contains(key) && !referenced.Contains(key))
                    errors.Add($"StrictOverride: {scope} Override '{key}' matches no payload path and is never " +
                               "referenced as a placeholder.");
        }

        Check("file-level", def.Override?.Keys);
        foreach (var step in def.Test.Steps)
            Check($"step '{step.Name}'", step.Override?.Keys);
    }

    private static HashSet<string> GatherReferencedTokens(PlaybookDefinition def, IPayloadProvider payloadProvider)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        void Scan(string? text)
        {
            if (text is null) return;
            foreach (Match m in VarReference.Matches(text))
            {
                var t = m.Groups[1].Value.Trim();
                if (!t.Equals("guid", StringComparison.OrdinalIgnoreCase) && !t.Equals("now", StringComparison.OrdinalIgnoreCase))
                    tokens.Add(t);
            }
        }

        Scan(new PlaybookLoader().Serialize(def));
        foreach (var send in def.Test.Steps.Select(s => s.Send).Where(s => s is { IsFileReference: true }))
            if (payloadProvider.Exists(send!.File!)) Scan(payloadProvider.ReadPayload(send.File!));

        return tokens;
    }

    private static HashSet<string> GatherPayloadPaths(PlaybookDefinition def, IPayloadProvider payloadProvider)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in def.Test.Steps)
        {
            JsonNode? node = null;
            if (step.Send?.Inline is not null)
                node = step.Send.Inline;
            else if (step.Send is { IsFileReference: true } fr && payloadProvider.Exists(fr.File!))
                node = JsonNode.Parse(payloadProvider.ReadPayload(fr.File!));

            PayloadResolver.CollectPaths(node, string.Empty, paths);
        }

        return paths;
    }
}
