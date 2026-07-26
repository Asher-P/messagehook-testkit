using KafkaFlow;
using Utf8Json;


namespace MessageHook.Kafka.Serializers;

public class KafkaUTF8Serializer : ISerializer, IDeserializer
{
    public Task SerializeAsync(object message, Stream output, ISerializerContext context) =>
        Utf8Json.JsonSerializer.NonGeneric.SerializeAsync(output, message);

    public Task<object> DeserializeAsync(Stream input, Type type, ISerializerContext context)
    {
        try
        {
            var resTask = JsonSerializer.NonGeneric.DeserializeAsync(type, input);
            var res = resTask.GetAwaiter().GetResult();
            return resTask;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}