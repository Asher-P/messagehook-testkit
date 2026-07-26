using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageHook.Playbook.Models;

/// <summary>
/// One entry of the file-level <c>ConsumeTopics</c> / <c>ProduceTopics</c> lists. Authored either as a bare
/// string (<c>"B"</c> — defaults to the Json serializer and a schema-less payload) or as a full object.
/// (De)serialized through <see cref="TopicDeclarationConverter"/> so both forms round-trip.
/// </summary>
[JsonConverter(typeof(TopicDeclarationConverter))]
public sealed class TopicDeclaration
{
    /// <summary>Kafka topic name — the identity used everywhere downstream.</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>Serializer name: <c>Json</c>/<c>Utf8</c> (default) or <c>Protobuf</c>.</summary>
    public string Serializer { get; set; } = "Json";

    /// <summary>
    /// Optional assembly-qualified .NET type (e.g. <c>Ns.Animal, MyAsm</c>). When omitted the topic is
    /// schema-less (<see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/> of string→object).
    /// Required for Protobuf, which cannot serialize a dictionary.
    /// </summary>
    public string? MessageType { get; set; }

    /// <summary>Consumer only. Overrides the KafkaFlow default worker count.</summary>
    public int? WorkersCount { get; set; }

    /// <summary>Consumer only. Overrides the KafkaFlow default buffer size.</summary>
    public int? BufferSize { get; set; }
}

/// <summary>Reads/writes <see cref="TopicDeclaration"/> as either a bare string or a full object.</summary>
public sealed class TopicDeclarationConverter : JsonConverter<TopicDeclaration>
{
    public override TopicDeclaration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new TopicDeclaration { Topic = reader.GetString() ?? string.Empty };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"A topic declaration must be a string or an object, got {reader.TokenType}.");

        var result = new TopicDeclaration();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var prop = reader.GetString();
            reader.Read();
            switch (prop?.ToLowerInvariant())
            {
                case "topic": result.Topic = reader.GetString() ?? string.Empty; break;
                case "serializer": result.Serializer = reader.GetString() ?? "Json"; break;
                case "messagetype": result.MessageType = reader.GetString(); break;
                case "workerscount": result.WorkersCount = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32(); break;
                case "buffersize": result.BufferSize = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32(); break;
                default: reader.Skip(); break;
            }
        }

        throw new JsonException("Unexpected end of JSON while reading a topic declaration.");
    }

    public override void Write(Utf8JsonWriter writer, TopicDeclaration value, JsonSerializerOptions options)
    {
        // Emit the compact string form when nothing but the topic name is set, so a UI round-trip stays terse.
        var isPlain = value.Serializer.Equals("Json", StringComparison.OrdinalIgnoreCase)
                      && value.MessageType is null
                      && value.WorkersCount is null
                      && value.BufferSize is null;
        if (isPlain)
        {
            writer.WriteStringValue(value.Topic);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("topic", value.Topic);
        writer.WriteString("serializer", value.Serializer);
        if (value.MessageType is not null) writer.WriteString("messageType", value.MessageType);
        if (value.WorkersCount is not null) writer.WriteNumber("workersCount", value.WorkersCount.Value);
        if (value.BufferSize is not null) writer.WriteNumber("bufferSize", value.BufferSize.Value);
        writer.WriteEndObject();
    }
}
