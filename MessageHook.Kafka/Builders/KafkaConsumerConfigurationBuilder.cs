using MessageHook.Kafka.Configurations;
using KafkaFlow;

namespace MessageHook.Kafka.Builders;

public class KafkaConsumerConfigurationBuilder : IKafkaConsumerConfigurationBuilder
{
    public string ConsumerGroup { get; set; }
    public string Topic { get; set; }
    public IDeserializer ConsumerDeserialization { get; set; }
    public Type ConsumingType { get; set; }
    public string ConsumerName { get; set; }
    public int? WorkersCount { get; set; } = null;
    public int? BufferSize { get; set; } = null;


    public IKafkaConsumerConfigurationBuilder AddTopic(string topicName)
    {
        Topic = topicName;
        return this;
    }

    public IKafkaConsumerConfigurationBuilder AddConsumingType(Type consumingType)
    {
        ConsumingType = consumingType;
        return this;
    }

    public IKafkaConsumerConfigurationBuilder AddConsumingSerializer(IDeserializer consumerSerializer)
    {
        ConsumerDeserialization = consumerSerializer;
        return this;
    }

    public IKafkaConsumerConfigurationBuilder AddConsumerGroup(string consumerGroupName)
    {
        ConsumerGroup = consumerGroupName;
        return this;
    }
    
    public IKafkaConsumerConfigurationBuilder SetWorkersCount(int workersCount)
    {
        WorkersCount = workersCount;
        return this;
    }
    public IKafkaConsumerConfigurationBuilder SetBufferSize(int bufferSize)
    {
        BufferSize = bufferSize;
        return this;
    }

    public KafkaConsumerConfiguration Build()
    {
        return new KafkaConsumerConfiguration()
        {
            ConsumerGroup = this.ConsumerGroup,
            ConsumerDeserialization = this.ConsumerDeserialization,
            ConsumingType = this.ConsumingType,
            Topic = this.Topic,
            BufferSize = this.BufferSize,
            WorkersCount = this.WorkersCount
        };
    }
}