using KafkaFlow;

namespace MessageHook.Kafka.Configurations;

public class KafkaProducerConfiguration
{
    public string ProducerTopic { get; set; }
    public ISerializer ProducerSerializer { get; set; }
    
}