using MessageHook.Playbook.Loading;
using MessageHook.Playbook.Results;
using Microsoft.Extensions.Logging;

namespace MessageHook.Playbook.Execution;

/// <summary>Knobs for a run. All optional; sensible defaults let a caller pass nothing.</summary>
public sealed class PlaybookRunOptions
{
    /// <summary>How <c>Send.file</c> references resolve. Defaults to the playbook's own directory (file runs)
    /// or an empty in-memory provider (definition/stream runs).</summary>
    public IPayloadProvider? PayloadProvider { get; set; }

    /// <summary>Overrides the base consumer group from the playbook (a per-run suffix is still appended).</summary>
    public string? ConsumerGroupOverride { get; set; }

    /// <summary>Receives each step result as it completes — for live UI updates.</summary>
    public IProgress<StepResult>? Progress { get; set; }

    public ILoggerFactory? LoggerFactory { get; set; }

    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}
