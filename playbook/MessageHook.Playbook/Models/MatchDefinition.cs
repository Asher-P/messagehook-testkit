namespace MessageHook.Playbook.Models;

public enum MatchMode
{
    /// <summary>Default. MessageHook auto-injects a correlation id header on produce and waits for it.</summary>
    CorrelationId,

    /// <summary>Wait for a message whose Kafka key equals <see cref="MatchDefinition.ExpectedKey"/>.</summary>
    MessageKey
}

/// <summary>How a step decides which consumed message is "its" response.</summary>
public sealed class MatchDefinition
{
    public MatchMode Mode { get; set; } = MatchMode.CorrelationId;

    /// <summary>Required (and only used) when <see cref="Mode"/> is <see cref="MatchMode.MessageKey"/>.</summary>
    public string? ExpectedKey { get; set; }
}
