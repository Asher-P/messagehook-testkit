using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MessageHook.Playbook.Models;

/// <summary>
/// A step's <c>Send</c> payload. Authored as an inline object, a bare file-path string, or the object form
/// <c>{ "file": "..." }</c>. A single-property <c>{ "file": &lt;string&gt; }</c> object is a file reference;
/// any richer object is treated as a literal inline payload (so a payload may itself contain a "file" field).
/// </summary>
[JsonConverter(typeof(SendDefinitionConverter))]
public sealed class SendDefinition
{
    /// <summary>Relative payload file reference, resolved through an <c>IPayloadProvider</c>. Null when inline.</summary>
    public string? File { get; set; }

    /// <summary>Literal inline payload. Null when a <see cref="File"/> reference is used.</summary>
    public JsonObject? Inline { get; set; }

    public bool IsFileReference => File is not null;
}

public sealed class SendDefinitionConverter : JsonConverter<SendDefinition>
{
    public override SendDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new SendDefinition { File = reader.GetString() };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"'Send' must be a string or an object, got {reader.TokenType}.");

        var node = JsonNode.Parse(ref reader) as JsonObject
                   ?? throw new JsonException("'Send' object could not be parsed.");

        // Exactly one property named "file" (string) => file reference; otherwise a literal inline payload.
        if (node.Count == 1 && node.TryGetPropertyValue("file", out var fileNode) && fileNode is JsonValue fv
            && fv.TryGetValue(out string? path))
        {
            return new SendDefinition { File = path };
        }

        return new SendDefinition { Inline = node };
    }

    public override void Write(Utf8JsonWriter writer, SendDefinition value, JsonSerializerOptions options)
    {
        if (value.IsFileReference)
        {
            writer.WriteStringValue(value.File);
            return;
        }

        (value.Inline ?? new JsonObject()).WriteTo(writer, options);
    }
}
