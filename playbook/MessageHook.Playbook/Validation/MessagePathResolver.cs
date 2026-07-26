using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MessageHook.Playbook.Validation;

/// <summary>
/// Navigates a message payload by path. Handles the shapes MessageHook actually produces: Utf8Json's
/// <c>Dictionary&lt;string, object&gt;</c> / lists / primitives, System.Text.Json nodes, and reflected POCOs
/// (when a topic declares a concrete <c>messageType</c>).
/// </summary>
public static class MessagePathResolver
{
    public static bool TryResolve(object? root, string? path, out object? value)
    {
        if (MessagePath.IsRoot(path))
        {
            value = Unwrap(root);
            return true;
        }

        object? current = root;
        foreach (var segment in MessagePath.Parse(path!))
        {
            if (!TryStep(current, segment, out current))
            {
                value = null;
                return false;
            }
        }

        value = Unwrap(current);
        return true;
    }

    private static bool TryStep(object? current, PathSegment segment, out object? next)
    {
        next = null;
        current = UnwrapContainer(current);
        if (current is null)
            return false;

        if (segment.IsIndex)
        {
            switch (current)
            {
                case JsonArray ja:
                    if (segment.Index < 0 || segment.Index >= ja.Count) return false;
                    next = ja[segment.Index];
                    return true;
                case string:
                    return false; // don't index into strings
                case IList list:
                    if (segment.Index < 0 || segment.Index >= list.Count) return false;
                    next = list[segment.Index];
                    return true;
                default:
                    return false;
            }
        }

        switch (current)
        {
            case JsonObject jo:
                if (jo.TryGetPropertyValue(segment.Name, out var node)) { next = node; return true; }
                return false;
            case IDictionary dict:
                if (dict.Contains(segment.Name)) { next = dict[segment.Name]; return true; }
                return false;
            default:
                // POCO via reflection (case-insensitive).
                var prop = current.GetType().GetProperty(segment.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (prop is not null) { next = prop.GetValue(current); return true; }
                return false;
        }
    }

    /// <summary>JsonElement of Object/Array kind isn't directly navigable above; convert it to a node once.</summary>
    private static object? UnwrapContainer(object? current) => current switch
    {
        JsonElement je => JsonNode.Parse(je.GetRawText()),
        _ => current
    };

    /// <summary>Reduces JSON wrappers to plain CLR scalars/containers for comparison and reporting.</summary>
    public static object? Unwrap(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonValue jv:
                if (jv.TryGetValue(out string? s)) return s;
                if (jv.TryGetValue(out bool b)) return b;
                if (jv.TryGetValue(out double d)) return d;
                if (jv.TryGetValue(out long l)) return l;
                return jv.ToJsonString().Trim('"');
            case JsonElement je:
                return je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString(),
                    JsonValueKind.Number => je.TryGetInt64(out var i) ? i : je.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => je // objects/arrays returned as-is; navigated earlier
                };
            default:
                return value;
        }
    }

    /// <summary>Formats a resolved value for result reporting.</summary>
    public static string? Format(object? value) => value switch
    {
        null => null,
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        JsonNode n => n.ToJsonString(),
        _ => value.ToString()
    };
}
