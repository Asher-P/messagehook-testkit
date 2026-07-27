namespace MessageHook.Playbook;

/// <summary>
/// Thrown for load/validation problems in a playbook (bad JSON, undeclared topic, conflicting match modes,
/// unresolvable payload, StrictOverride violations). Carries all discovered errors, not just the first.
/// </summary>
public sealed class PlaybookException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public PlaybookException(string message) : base(message) => Errors = new[] { message };

    public PlaybookException(IReadOnlyList<string> errors)
        : base("Playbook is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
        => Errors = errors;
}
