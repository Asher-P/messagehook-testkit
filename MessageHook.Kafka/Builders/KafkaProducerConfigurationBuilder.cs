using MessageHook.Kafka.Configurations;
using KafkaFlow;

namespace MessageHook.Kafka.Builders;

public class KafkaProducerConfigurationBuilder : IKafkaProducerConfigurationBuilder
{
    public string ProducerTopic { get; set; }
    public ISerializer ProducerSerializer { get; set; }
  
    public IKafkaProducerConfigurationBuilder AddProducerTopic(string topicName)
    {
        ProducerTopic = topicName;
        return this;
    }

    public IKafkaProducerConfigurationBuilder AddProducerSerializer(ISerializer producerSerializer)
    {
        ProducerSerializer = producerSerializer;
        return this;
    }

    public KafkaProducerConfiguration Build()
    {
        return new KafkaProducerConfiguration()
        {
            ProducerTopic = this.ProducerTopic,
            ProducerSerializer = this.ProducerSerializer
        };
    }
}