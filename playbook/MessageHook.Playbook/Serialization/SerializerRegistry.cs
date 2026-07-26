using KafkaFlow;
using MessageHook.Kafka.Serializers;

namespace MessageHook.Playbook.Serialization;

/// <summary>
/// Maps playbook serializer names onto the existing MessageHook.Kafka serializer instances, and resolves an
/// optional assembly-qualified <c>messageType</c> to a .NET <see cref="Type"/>.
/// </summary>
/// <remarks>
/// Only <see cref="KafkaUTF8Serializer"/> implements <see cref="IDeserializer"/>, so consuming is Json-only;
/// <see cref="KafkaProtobufSerializer"/> is produce-only. The default consuming type is a schema-less
/// <c>Dictionary&lt;string, object&gt;</c>, which Utf8Json's non-generic path fills with nested dictionaries,
/// lists, numbers and strings.
/// </remarks>
public sealed class SerializerRegistry
{
    private static readonly KafkaUTF8Serializer Utf8 = new();
    private static readonly KafkaProtobufSerializer Protobuf = new();

    public static readonly Type SchemaLessType = typeof(Dictionary<string, object>);

    public ISerializer GetProducerSerializer(string name) => Normalize(name) switch
    {
        "json" or "utf8" => Utf8,
        "protobuf" => Protobuf,
        _ => throw new PlaybookException($"Unknown producer serializer '{name}'. Use Json/Utf8 or Protobuf.")
    };

    public IDeserializer GetConsumerDeserializer(string name) => Normalize(name) switch
    {
        "json" or "utf8" => Utf8,
        "protobuf" => throw new PlaybookException(
            "Protobuf consuming is not supported (no deserializer in MessageHook.Kafka); use Json on consume topics."),
        _ => throw new PlaybookException($"Unknown consumer serializer '{name}'. Use Json/Utf8.")
    };

    /// <summary>Resolves a topic's <c>messageType</c> to a Type, or the schema-less dictionary when absent.</summary>
    public Type ResolveType(string? messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            return SchemaLessType;

        return Type.GetType(messageType, throwOnError: false)
               ?? throw new PlaybookException(
                   $"Could not load messageType '{messageType}'. Use an assembly-qualified name, " +
                   "e.g. 'My.Ns.Animal, My.Assembly', and ensure the assembly is referenced.");
    }

    private static string Normalize(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();
}
