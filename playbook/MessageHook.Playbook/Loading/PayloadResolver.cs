using System.Text.Json.Nodes;
using MessageHook.Playbook.Execution;
using MessageHook.Playbook.Models;
using MessageHook.Playbook.Validation;

namespace MessageHook.Playbook.Loading;

/// <summary>
/// Turns a step's <c>Send</c> + effective <c>Override</c> into the final payload node, and feeds placeholder
/// variables into the run context. Override does double duty: an entry matching a payload path replaces that
/// value; an entry matching nothing becomes a <c>{{name}}</c> variable.
/// </summary>
public sealed class PayloadResolver
{
    private readonly PlaceholderExpander _expander;

    public PayloadResolver(PlaceholderExpander expander) => _expander = expander;

    /// <summary>
    /// Resolves the final payload for a step. Returns null for a consume-only step (no <c>Send</c>), in which
    /// case every override entry is registered as a placeholder variable.
    /// </summary>
    public JsonNode? Resolve(
        SendDefinition? send,
        IReadOnlyDictionary<string, JsonNode?> effectiveOverrides,
        PlaybookContext context,
        IPayloadProvider payloadProvider)
    {
        JsonNode? payload = LoadPayload(send, payloadProvider);

        var matches = new List<(string Path, JsonNode? Value)>();

        foreach (var (key, rawValue) in effectiveOverrides)
        {
            var expanded = ExpandOverrideValue(rawValue, context);

            if (payload is not null && PathExists(payload, key))
                matches.Add((key, expanded));
            else
                context.SetVariable(key, ToStringValue(expanded));
        }

        if (payload is null)
            return null;

        // Expand placeholders inside the payload now that variables are registered, then apply the patches.
        payload = _expander.ExpandNode(payload, context.Variables);
        foreach (var (path, value) in matches)
            SetPath(payload!, path, value?.DeepClone());

        return payload;
    }

    private JsonNode? LoadPayload(SendDefinition? send, IPayloadProvider provider)
    {
        if (send is null)
            return null;

        if (send.Inline is not null)
            return send.Inline.DeepClone();

        var text = provider.ReadPayload(send.File!);
        return JsonNode.Parse(text)
               ?? throw new PlaybookException($"Payload '{send.File}' is empty or not valid JSON.");
    }

    private JsonNode? ExpandOverrideValue(JsonNode? value, PlaybookContext context)
    {
        if (value is JsonValue v && v.TryGetValue(out string? s))
            return JsonValue.Create(_expander.ExpandString(s, context.Variables));
        return value?.DeepClone();
    }

    private static string ToStringValue(JsonNode? value)
    {
        if (value is null) return string.Empty;
        if (value is JsonValue v && v.TryGetValue(out string? s)) return s;
        return value.ToJsonString();
    }

    // --- JsonNode path navigation (payloads are static JSON trees) -------------------------------------

    /// <summary>True when <paramref name="path"/> addresses an existing node in the payload tree.</summary>
    public static bool PathExists(JsonNode root, string path)
    {
        var segments = MessagePath.Parse(path);
        if (segments.Count == 0)
            return true;

        JsonNode? current = root;
        foreach (var seg in segments)
        {
            switch (current)
            {
                case JsonObject obj when !seg.IsIndex && obj.TryGetPropertyValue(seg.Name, out var child):
                    current = child;
                    break;
                case JsonArray arr when seg.IsIndex && seg.Index >= 0 && seg.Index < arr.Count:
                    current = arr[seg.Index];
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    public static void SetPath(JsonNode root, string path, JsonNode? value)
    {
        var segments = MessagePath.Parse(path);
        if (segments.Count == 0) return;

        JsonNode? current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var seg = segments[i];
            current = seg.IsIndex
                ? (current as JsonArray)?[seg.Index]
                : (current as JsonObject)?[seg.Name];
            if (current is null) return;
        }

        var lastSeg = segments[^1];
        if (lastSeg.IsIndex && current is JsonArray arr && lastSeg.Index >= 0 && lastSeg.Index < arr.Count)
            arr[lastSeg.Index] = value;
        else if (!lastSeg.IsIndex && current is JsonObject obj)
            obj[lastSeg.Name] = value;
    }

    /// <summary>Flattens a payload tree to the set of dotted paths it contains (used by StrictOverride checks).</summary>
    public static void CollectPaths(JsonNode? node, string prefix, ISet<string> paths)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    var path = prefix.Length == 0 ? kv.Key : $"{prefix}.{kv.Key}";
                    paths.Add(path);
                    CollectPaths(kv.Value, path, paths);
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    var path = $"{prefix}[{i}]";
                    paths.Add(path);
                    CollectPaths(arr[i], path, paths);
                }
                break;
        }
    }
}
