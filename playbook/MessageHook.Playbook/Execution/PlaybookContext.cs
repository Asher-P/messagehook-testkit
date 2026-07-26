namespace MessageHook.Playbook.Execution;

/// <summary>
/// Carries variables across steps: Override entries that act as placeholder values, plus values captured from
/// received messages. Steps run sequentially, so later steps see earlier captures.
/// </summary>
public sealed class PlaybookContext
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Variables => _variables;

    public void SetVariable(string name, string value) => _variables[name] = value;
}
