using System.Text.Json.Nodes;
using MessageHook.Kafka.Configurations;

namespace MessageHook.Playbook.Models;

/// <summary>Broker connection for the whole suite. The runner connects to an existing Kafka; it never provisions one.</summary>
public sealed class KafkaConfigDefinition
{
    public List<string> BootstrapServers { get; set; } = new();

    public KafkaCredentialsConfiguration Credentials { get; set; } = new();

    /// <summary>Base consumer group. A per-run suffix is appended so repeated/parallel runs don't collide.</summary>
    public string ConsumerGroup { get; set; } = "messagehook-playbook";

    public KafkaBrokerConfiguration ToBrokerConfiguration() => new()
    {
        BootstrapServers = BootstrapServers,
        Credentials = Credentials
    };
}

/// <summary>The whole playbook: broker config, declared topics, and an ordered list of steps.</summary>
public sealed class PlaybookDefinition
{
    public string? Name { get; set; }

    public KafkaConfigDefinition KafkaConfiguration { get; set; } = new();

    public List<TopicDeclaration> ConsumeTopics { get; set; } = new();

    public List<TopicDeclaration> ProduceTopics { get; set; } = new();

    /// <summary>File-level overrides applied to every step (a step-level Override entry wins).</summary>
    public Dictionary<string, JsonNode?>? Override { get; set; }

    /// <summary>When true, an Override entry matching neither a payload path nor any placeholder is a load-time error.</summary>
    public bool StrictOverride { get; set; }

    public PlaybookTest Test { get; set; } = new();
}
