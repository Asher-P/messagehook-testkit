using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MessageHook.Orchestration.Entities.Enums;

namespace MessageHook.Playbook.Models;

/// <summary>One produce/consume step of a playbook. Steps run sequentially.</summary>
public sealed class PlaybookStep
{
    public string? Name { get; set; }

    /// <summary>Topic to produce to. Must be declared in the file-level <c>ProduceTopics</c>.
    /// Omit for a consume-only step.</summary>
    public string? ProduceTo { get; set; }

    /// <summary>Topics to wait on. Each must be declared in the file-level <c>ConsumeTopics</c>.</summary>
    public List<string>? ConsumeFrom { get; set; }

    /// <summary>Kafka message key for the produced message. Placeholders allowed.</summary>
    public string? Key { get; set; }

    /// <summary>Extra headers on the produced message. Values may contain placeholders.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>The payload to send (inline or external file). Omit for a consume-only step.</summary>
    public SendDefinition? Send { get; set; }

    /// <summary>
    /// Step-level overrides. An entry whose name matches a payload path replaces that value; an entry that
    /// matches nothing becomes a placeholder value usable as <c>{{name}}</c>. Wins over file-level Override.
    /// </summary>
    public Dictionary<string, JsonNode?>? Override { get; set; }

    public MatchDefinition? Match { get; set; }

    /// <summary>
    /// How many messages the step waits for; null means the default of 1. Meaningless when <see cref="HookType"/>
    /// is <see cref="MessageHookType.ProduceAndForget"/> — that shape never waits, so <see cref="Normalize"/>
    /// clears it and the effective count is 0.
    /// </summary>
    public int? ExpectedMessageCount { get; set; }

    /// <summary>
    /// The shape of this step, derived from the topics with the same rule the engine uses in
    /// <c>BaseMessageHookStep.MessageHookType</c>: no <see cref="ConsumeFrom"/> is fire-and-forget, otherwise
    /// a <see cref="ProduceTo"/> makes it a round trip and its absence makes it consume-only.
    /// </summary>
    [JsonIgnore]
    public MessageHookType HookType =>
        ConsumeFrom is not { Count: > 0 } ? MessageHookType.ProduceAndForget
        : !string.IsNullOrWhiteSpace(ProduceTo) ? MessageHookType.ProduceAndWait
        : MessageHookType.ConsumeOnly;

    /// <summary>The count actually waited for: 0 for fire-and-forget, <see cref="ExpectedMessageCount"/> otherwise.</summary>
    [JsonIgnore]
    public int EffectiveMessageCount =>
        HookType == MessageHookType.ProduceAndForget ? 0 : ExpectedMessageCount ?? 1;

    /// <summary>
    /// Drops what this step's shape cannot use, so a stored or re-emitted step reads the way it runs: a
    /// fire-and-forget step never waits, so it carries no <see cref="ExpectedMessageCount"/>. Applied on load
    /// and on save, which is what retires the old "produce-only means count 0" convention from existing files.
    /// </summary>
    public void Normalize()
    {
        if (HookType == MessageHookType.ProduceAndForget)
            ExpectedMessageCount = null;
    }

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Capture values from the received message into named variables: <c>{ "var": "path.in.payload" }</c>.</summary>
    public Dictionary<string, string>? Capture { get; set; }

    public List<ValidationDefinition>? Validations { get; set; }
}

public sealed class PlaybookTest
{
    public List<PlaybookStep> Steps { get; set; } = new();
}
