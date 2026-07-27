using MessageHook.Kafka.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace MessageHook.Kafka.Extensions;

public static class KafkaConfigurationExtension
{
    public static IServiceCollection AddKafkaConfiguration(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IKafkaConfigurationBuilder, KafkaConfigurationBuilder>();
        return serviceCollection;
    }

  
}