using KafkaFlow;

namespace MessageHook.EchoService.Serializers;

public class RawBytesSerializer : ISerializer, IDeserializer
{
    public Task SerializeAsync(object message, Stream output, ISerializerContext context)
    {
        var bytes = (byte[])message;
        return output.WriteAsync(bytes, 0, bytes.Length);
    }

    public async Task<object> DeserializeAsync(Stream input, Type type, ISerializerContext context)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms);
        return ms.ToArray();
    }
}
