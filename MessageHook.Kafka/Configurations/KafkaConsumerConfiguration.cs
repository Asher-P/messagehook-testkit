using KafkaFlow;

namespace MessageHook.Kafka.Configurations;

public class KafkaConsumerConfiguration
{
    public string ConsumerGroup { get; set; }
    public string Topic { get; set; }
    public IDeserializer ConsumerDeserialization { get; set; }
    public Type ConsumingType { get; set; }
    public int? WorkersCount { get; set; } = null;
    public int? BufferSize { get; set; } = null;
}