using System.Collections.Concurrent;
using System.Text;
using MessageHook.Playbook.Hosting;
using MessageHook.Playbook.Loading;
using MessageHook.Playbook.Models;
using MessageHook.Playbook.Results;
using MessageHook.Playbook.Serialization;
using MessageHook.Playbook.Validation;

namespace MessageHook.Playbook.Execution;

/// <summary>
/// The public entry point. Host-agnostic: run from a file path, a stream, or an in-memory
/// <see cref="PlaybookDefinition"/>. <see cref="Validate"/> performs load-time checks with no broker.
///
/// Safe to call repeatedly in one process: a <see cref="PlaybookHost"/> (KafkaFlow bus) is built once per Kafka
/// config and reused across runs, never stopped or disposed. That reuse is not just an optimization — stopping
/// or disposing a KafkaFlow consumer crashes the process with a native access violation (see
/// <see cref="PlaybookHost"/>), so a long-lived service must keep each bus running. Hosts are cached for the
/// life of the runner (a singleton in the web service), one per distinct broker+topics config.
/// </summary>
public sealed class PlaybookRunner
{
    private readonly PlaybookLoader _loader = new();
    private readonly PlaceholderExpander _expander = new();
    private readonly SerializerRegistry _serializers = new();
    private readonly ValidationRuleRegistry _rules = new();
    private readonly PayloadResolver _payloadResolver;
    private readonly PlaybookValidator _validator;
    private readonly ConcurrentDictionary<string, Lazy<PlaybookHost>> _hosts = new();

    public PlaybookRunner()
    {
        _payloadResolver = new PayloadResolver(_expander);
        _validator = new PlaybookValidator(_serializers, _rules);
    }

    /// <summary>Loads a playbook from disk and runs it. Payload files resolve relative to the playbook's directory.</summary>
    public async Task<PlaybookResult> RunAsync(string playbookPath, PlaybookRunOptions? options = null)
    {
        var definition = _loader.LoadFromFile(playbookPath);
        options ??= new PlaybookRunOptions();
        options.PayloadProvider ??= new FileSystemPayloadProvider(
            Path.GetDirectoryName(Path.GetFullPath(playbookPath)) ?? Directory.GetCurrentDirectory());
        return await RunAsync(definition, options);
    }

    /// <summary>Loads a playbook from a stream and runs it. Provide a PayloadProvider in options for file payloads.</summary>
    public async Task<PlaybookResult> RunAsync(Stream playbookJson, PlaybookRunOptions? options = null)
    {
        var definition = _loader.LoadFromStream(playbookJson);
        return await RunAsync(definition, options ?? new PlaybookRunOptions());
    }

    /// <summary>Runs an in-memory definition — the primitive a web service uses for a posted playbook.</summary>
    public async Task<PlaybookResult> RunAsync(PlaybookDefinition definition, PlaybookRunOptions? options = null)
    {
        options ??= new PlaybookRunOptions();
        var payloadProvider = options.PayloadProvider ?? new InMemoryPayloadProvider(new Dictionary<string, string>());

        // Broker-free validation first — nothing connects to Kafka until this passes.
        _validator.Validate(definition, payloadProvider);

        var result = new PlaybookResult { Name = definition.Name };
        var context = new PlaybookContext();

        // Reuse a cached bus for this Kafka config (never stopped/disposed — see the type remark).
        var host = GetOrCreateHost(definition, options);

        var executor = new StepExecutor(
            host, _payloadResolver, _expander, _rules, _serializers, definition, payloadProvider);

        try
        {
            foreach (var step in definition.Test.Steps)
            {
                options.CancellationToken.ThrowIfCancellationRequested();

                var stepResult = await executor.ExecuteAsync(step, context, options.CancellationToken);
                result.Steps.Add(stepResult);
                options.Progress?.Report(stepResult);

                // A hard failure (timeout, produce error) can invalidate later steps that depend on captures.
                if (stepResult.Error is not null)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            result.Error = "Run cancelled.";
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Returns the cached host for this definition's Kafka config, building it once on first use. The same
    /// broker + declared topics + consumer group always map to the same running bus, so repeated runs of a suite
    /// share one set of consumers instead of spinning up (and having to tear down) a bus each time.
    /// </summary>
    private PlaybookHost GetOrCreateHost(PlaybookDefinition definition, PlaybookRunOptions options)
    {
        var key = HostKey(definition, options.ConsumerGroupOverride);
        var lazy = _hosts.GetOrAdd(key, _ => new Lazy<PlaybookHost>(
            () => new PlaybookHost(definition, _expander, _serializers, options.ConsumerGroupOverride, options.LoggerFactory)));
        return lazy.Value;
    }

    /// <summary>
    /// Identity of a host: everything that shapes the KafkaFlow bus. Two definitions with the same value here can
    /// safely share a host; anything that differs (a new topic, a different broker) gets its own.
    /// </summary>
    private static string HostKey(PlaybookDefinition d, string? groupOverride)
    {
        var creds = d.KafkaConfiguration.Credentials;
        var sb = new StringBuilder();
        sb.Append(string.Join(",", d.KafkaConfiguration.BootstrapServers));
        sb.Append('|').Append(creds.SecurityProtocol).Append(';').Append(creds.Mechanism)
          .Append(';').Append(creds.Username).Append(';').Append(creds.TlsEnabled);
        sb.Append('|').Append(groupOverride ?? d.KafkaConfiguration.ConsumerGroup);
        sb.Append('|').Append(string.Join(",", d.ConsumeTopics.Select(TopicKey)));
        sb.Append('|').Append(string.Join(",", d.ProduceTopics.Select(TopicKey)));
        return sb.ToString();

        static string TopicKey(TopicDeclaration t) =>
            $"{t.Topic}:{t.Serializer}:{t.MessageType}:{t.WorkersCount}:{t.BufferSize}";
    }

    /// <summary>Broker-free load+validate. Throws <see cref="PlaybookException"/> if the playbook is invalid.</summary>
    public void Validate(PlaybookDefinition definition, IPayloadProvider? payloadProvider = null) =>
        _validator.Validate(definition, payloadProvider ?? new InMemoryPayloadProvider(new Dictionary<string, string>()));

    /// <summary>Loads and validates JSON text without running it — for a "validate as you type" UI.</summary>
    public void Validate(string json, IPayloadProvider? payloadProvider = null) =>
        Validate(_loader.Load(json), payloadProvider);
}
