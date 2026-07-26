using MessageHook.Core.Messaging.Consuming;
using MessageHook.Core.Messaging.Publishing;
using MessageHook.Kafka.Builders;
using MessageHook.Kafka.Consumers;
using MessageHook.Kafka.Producers;
using MessageHook.Orchestration.Host;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHook.Kafka.Extensions;

public static class HostExtensions
{
    public static IServiceCollection AddKafkaConsumer(this IServiceCollection services)
    {
        services.AddSingleton<IConsumer, KafkaConsumer>();
        return services;
    }
    public static IServiceCollection AddKafkaMessageHook(this IServiceCollection serviceCollection,Action<IKafkaConfigurationBuilder> kafkaConfigurationBuilder)
    {
        serviceCollection.AddMessageHookService()
            .AddSingleton<IConsumer, KafkaConsumer>()
            .AddSingleton<IProducer, KafkaProducer>();
        serviceCollection.AddSingleton<IKafkaConfigurationBuilder, KafkaConfigurationBuilder>();
        var builder = new KafkaConfigurationBuilder();
        kafkaConfigurationBuilder.Invoke(builder);
        builder.BuildKafkaFlow(serviceCollection);
        return serviceCollection;
    }
    
   
}