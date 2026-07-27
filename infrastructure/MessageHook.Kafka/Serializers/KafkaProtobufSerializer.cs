using KafkaFlow;

namespace MessageHook.Kafka.Serializers;

public class KafkaProtobufSerializer : ISerializer
{
    public Task SerializeAsync(object message, Stream output, ISerializerContext context)
        {
            ProtoBuf.Serializer.Serialize(output, message);
            return Task.CompletedTask;
        }
}