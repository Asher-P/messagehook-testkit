using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace MessageHook.Playbook.Loading;

/// <summary>
/// Expands placeholders in playbook strings:
/// <list type="bullet">
///   <item><c>${ENV}</c> or <c>${ENV:default}</c> — environment variable (default after the first colon);</item>
///   <item><c>{{guid}}</c> — a fresh GUID; <c>{{now}}</c> — UTC ISO-8601 timestamp;</item>
///   <item><c>{{name}}</c> — a variable from Override entries or an earlier step's Capture.</item>
/// </list>
/// </summary>
public sealed class PlaceholderExpander
{
    private static readonly Regex EnvPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex VarPattern = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    public string ExpandString(string? input, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        var afterEnv = EnvPattern.Replace(input, m =>
        {
            var token = m.Groups[1].Value;
            var sep = token.IndexOf(':');
            var name = sep >= 0 ? token[..sep] : token;
            var fallback = sep >= 0 ? token[(sep + 1)..] : null;
            var value = Environment.GetEnvironmentVariable(name.Trim());
            if (!string.IsNullOrEmpty(value)) return value;
            if (fallback is not null) return fallback;
            throw new PlaybookException($"Environment variable '{name.Trim()}' is not set and has no default (${{{token}}}).");
        });

        return VarPattern.Replace(afterEnv, m =>
        {
            var name = m.Groups[1].Value.Trim();
            return name.ToLowerInvariant() switch
            {
                "guid" => Guid.NewGuid().ToString(),
                "now" => DateTime.UtcNow.ToString("O"),
                _ => variables.TryGetValue(name, out var v)
                    ? v
                    : throw new PlaybookException($"Unknown placeholder '{{{{{name}}}}}' — no matching variable or capture.")
            };
        });
    }

    /// <summary>Recursively expands placeholders inside every string value of a JSON tree, returning a new tree.</summary>
    public JsonNode? ExpandNode(JsonNode? node, IReadOnlyDictionary<string, string> variables)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
                var newObj = new JsonObject();
                foreach (var kv in obj)
                    newObj[kv.Key] = ExpandNode(kv.Value, variables);
                return newObj;
            case JsonArray arr:
                var newArr = new JsonArray();
                foreach (var item in arr)
                    newArr.Add(ExpandNode(item, variables));
                return newArr;
            case JsonValue val:
                if (val.TryGetValue(out string? s))
                    return JsonValue.Create(ExpandString(s, variables));
                return val.DeepClone();
            default:
                return node.DeepClone();
        }
    }
}
