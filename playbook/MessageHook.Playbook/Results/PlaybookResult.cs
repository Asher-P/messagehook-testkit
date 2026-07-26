namespace MessageHook.Playbook.Results;

/// <summary>
/// Outcome of a whole playbook run. Plain JSON-serializable DTO — safe to return to a UI verbatim.
/// </summary>
public sealed class PlaybookResult
{
    public string? Name { get; set; }
    public bool Passed => Steps.All(s => s.Passed);
    public List<StepResult> Steps { get; set; } = new();

    /// <summary>Set when the run failed before/around step execution (e.g. broker connect, cleanup).</summary>
    public string? Error { get; set; }

    public string Summary()
    {
        var passed = Steps.Count(s => s.Passed);
        var lines = new List<string> { $"Playbook '{Name}': {passed}/{Steps.Count} steps passed." };
        if (Error is not null) lines.Add($"Run error: {Error}");
        foreach (var step in Steps.Where(s => !s.Passed))
        {
            lines.Add($"  ✗ step '{step.Name}': {step.Error ?? "validation failed"}");
            foreach (var v in step.Validations.Where(v => !v.Passed))
                lines.Add($"      - {v.Describe()}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class StepResult
{
    public string? Name { get; set; }
    public bool Passed { get; set; }

    /// <summary>Set when the step threw (timeout, produce failure, …) rather than failing a validation.</summary>
    public string? Error { get; set; }

    public int ReceivedMessageCount { get; set; }
    public List<ValidationResult> Validations { get; set; } = new();
}

public sealed class ValidationResult
{
    public string? Target { get; set; }
    public string? Path { get; set; }
    public string? Type { get; set; }
    public string? Expected { get; set; }
    public string? Actual { get; set; }
    public bool Passed { get; set; }

    public string Describe() =>
        Passed
            ? $"{Target}:{Path} {Type} OK"
            : $"{Target}:{Path} {Type} — expected [{Expected}], actual [{Actual}]";
}
