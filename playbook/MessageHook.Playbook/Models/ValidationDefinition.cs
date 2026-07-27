using System.Text.Json.Nodes;

namespace MessageHook.Playbook.Models;

public enum ValidationTarget
{
    /// <summary>Default. Assert against the consumed message value.</summary>
    Value,

    /// <summary>Assert against the consumed message key (UTF8-decoded).</summary>
    Key
}

/// <summary>One structured assertion against a consumed message.</summary>
public sealed class ValidationDefinition
{
    public ValidationTarget Target { get; set; } = ValidationTarget.Value;

    /// <summary>Path into the payload: <c>a.b[0].c</c>. Empty or <c>$</c> means the whole payload.</summary>
    public string? Path { get; set; }

    /// <summary>
    /// Rule name: Equals, NotEquals, Contains, NotContains, Exists, NotExists, Matches,
    /// GreaterThan, LessThan, Count.
    /// </summary>
    public string Type { get; set; } = "Equals";

    /// <summary>Comparison operand. Unused by Exists/NotExists. May be a string, number, or bool.</summary>
    public JsonNode? Expected { get; set; }
}
