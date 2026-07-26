using System.Collections;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MessageHook.Playbook.Validation;

/// <summary>A single comparison operator usable in a playbook <c>Validations[].type</c>.</summary>
public interface IValidationRule
{
    string Name { get; }

    /// <summary><paramref name="found"/> is whether the path resolved; <paramref name="actual"/> is its
    /// unwrapped value; <paramref name="expected"/> is the operand (null for Exists/NotExists).</summary>
    bool IsSatisfied(bool found, object? actual, JsonNode? expected);
}

/// <summary>Shared coercion/comparison helpers so numeric "5" and 5 compare equal, etc.</summary>
internal static class Compare
{
    public static object? ExpectedScalar(JsonNode? expected)
    {
        if (expected is not JsonValue v) return expected?.ToJsonString();
        if (v.TryGetValue(out string? s)) return s;
        if (v.TryGetValue(out bool b)) return b;
        if (v.TryGetValue(out long l)) return l;
        if (v.TryGetValue(out double d)) return d;
        return v.ToJsonString().Trim('"');
    }

    public static bool TryNumber(object? value, out double number)
    {
        switch (value)
        {
            case double d: number = d; return true;
            case float f: number = f; return true;
            case long l: number = l; return true;
            case int i: number = i; return true;
            case short sh: number = sh; return true;
            case decimal m: number = (double)m; return true;
            case string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                number = parsed; return true;
            default:
                number = 0; return false;
        }
    }

    public static bool ValuesEqual(object? actual, JsonNode? expected)
    {
        var exp = ExpectedScalar(expected);
        if (actual is null || exp is null) return actual is null && exp is null;

        if (TryNumber(actual, out var an) && TryNumber(exp, out var en))
            return an.Equals(en);

        if (actual is bool ab && exp is bool eb)
            return ab == eb;

        return string.Equals(AsString(actual), AsString(exp), StringComparison.Ordinal);
    }

    public static string AsString(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    public static int? CountOf(object? actual) => actual switch
    {
        null => null,
        string => null,
        ICollection c => c.Count,
        IEnumerable e => e.Cast<object?>().Count(),
        _ => null
    };
}

internal sealed class EqualsRule : IValidationRule
{
    public string Name => "Equals";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected) => found && Compare.ValuesEqual(actual, expected);
}

internal sealed class NotEqualsRule : IValidationRule
{
    public string Name => "NotEquals";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected) => !(found && Compare.ValuesEqual(actual, expected));
}

internal sealed class ContainsRule : IValidationRule
{
    public string Name => "Contains";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected)
    {
        if (!found) return false;
        var exp = Compare.ExpectedScalar(expected);
        if (actual is string s) return s.Contains(Compare.AsString(exp), StringComparison.Ordinal);
        if (actual is IEnumerable e and not string)
            return e.Cast<object?>().Any(item => Compare.ValuesEqual(MessagePathResolver.Unwrap(item), expected));
        return false;
    }
}

internal sealed class NotContainsRule : IValidationRule
{
    public string Name => "NotContains";
    private readonly ContainsRule _contains = new();
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected) => !_contains.IsSatisfied(found, actual, expected);
}

internal sealed class ExistsRule : IValidationRule
{
    public string Name => "Exists";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected) => found && actual is not null;
}

internal sealed class NotExistsRule : IValidationRule
{
    public string Name => "NotExists";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected) => !found || actual is null;
}

internal sealed class MatchesRule : IValidationRule
{
    public string Name => "Matches";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected)
    {
        if (!found || actual is null) return false;
        var pattern = Compare.AsString(Compare.ExpectedScalar(expected));
        return Regex.IsMatch(Compare.AsString(actual), pattern);
    }
}

internal sealed class GreaterThanRule : IValidationRule
{
    public string Name => "GreaterThan";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected)
        => found && Compare.TryNumber(actual, out var a) && Compare.TryNumber(Compare.ExpectedScalar(expected), out var e) && a > e;
}

internal sealed class LessThanRule : IValidationRule
{
    public string Name => "LessThan";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected)
        => found && Compare.TryNumber(actual, out var a) && Compare.TryNumber(Compare.ExpectedScalar(expected), out var e) && a < e;
}

internal sealed class CountRule : IValidationRule
{
    public string Name => "Count";
    public bool IsSatisfied(bool found, object? actual, JsonNode? expected)
    {
        if (!found) return false;
        var count = Compare.CountOf(actual);
        return count is not null && Compare.TryNumber(Compare.ExpectedScalar(expected), out var e) && count == (int)e;
    }
}

public sealed class ValidationRuleRegistry
{
    private readonly Dictionary<string, IValidationRule> _rules;

    public ValidationRuleRegistry()
    {
        _rules = new IValidationRule[]
        {
            new EqualsRule(), new NotEqualsRule(), new ContainsRule(), new NotContainsRule(),
            new ExistsRule(), new NotExistsRule(), new MatchesRule(),
            new GreaterThanRule(), new LessThanRule(), new CountRule()
        }.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsKnown(string type) => _rules.ContainsKey(type);

    public IValidationRule Get(string type) =>
        _rules.TryGetValue(type, out var rule)
            ? rule
            : throw new PlaybookException($"Unknown validation type '{type}'. Known: {string.Join(", ", _rules.Keys)}.");

    public IEnumerable<string> KnownTypes => _rules.Keys;
}
