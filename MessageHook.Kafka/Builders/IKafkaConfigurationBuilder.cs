using MessageHook.Kafka.Configurations;
using KafkaFlow;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace MessageHook.Kafka.Builders;

public interface IKafkaConfigurationBuilder
{
    IKafkaConfigurationBuilder SetBrokerConfiguration(KafkaBrokerConfiguration brokerConfiguration);
    IKafkaConfigurationBuilder ConfigureBroker(Action<IKafkaBrokerConfigurationBuilder> brokerConfigurationBuilder);
    IKafkaConfigurationBuilder AddProducer(Action<IKafkaProducerConfigurationBuilder> producerBuilder);
    IKafkaConfigurationBuilder AddConsumer(Action<IKafkaConsumerConfigurationBuilder> consumerBuilder);
    IServiceCollection BuildKafkaFlow(IServiceCollection serviceCollection);
}

public interface IKafkaProducerConfigurationBuilder
{
    public string ProducerTopic { get; set; }
    public ISerializer ProducerSerializer { get; set; }
    
    IKafkaProducerConfigurationBuilder AddProducerTopic(string topicName);
    IKafkaProducerConfigurationBuilder AddProducerSerializer(ISerializer producerSerializer);
    KafkaProducerConfiguration Build();

}

public interface IKafkaConsumerConfigurationBuilder
{
    public string ConsumerGroup { get; set; }
    public string Topic { get; set; }
    public IDeserializer ConsumerDeserialization { get; set; }
    public Type ConsumingType { get; set; }
    public string ConsumerName { get; set; }
    public int? WorkersCount { get; set; }
    public int? BufferSize { get; set; }

    IKafkaConsumerConfigurationBuilder AddTopic(string topicName);
    IKafkaConsumerConfigurationBuilder AddConsumingType(Type consumingType);
    IKafkaConsumerConfigurationBuilder AddConsumingSerializer(IDeserializer consumerSerializer);
    IKafkaConsumerConfigurationBuilder AddConsumerGroup(string consumerGroupName);
    IKafkaConsumerConfigurationBuilder SetBufferSize(int bufferSize);
    IKafkaConsumerConfigurationBuilder SetWorkersCount(int workersCount);


    KafkaConsumerConfiguration Build();


}