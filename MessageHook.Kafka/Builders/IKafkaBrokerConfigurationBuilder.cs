using MessageHook.Kafka.Configurations;
using System.Collections.Generic;

namespace MessageHook.Kafka.Builders;

public interface IKafkaBrokerConfigurationBuilder
{
    IKafkaBrokerConfigurationBuilder WithBootstrapServers(IEnumerable<string> bootstrapServers);
    IKafkaBrokerConfigurationBuilder WithBootstrapServer(string bootstrapServer);
    IKafkaBrokerConfigurationBuilder WithCredentials(KafkaCredentialsConfiguration credentials);
    IKafkaBrokerConfigurationBuilder WithCredentials(string username, string password, string mechanism = "plain", string securityProtocol = "sasl_plaintext", bool tlsEnabled = false);
    KafkaBrokerConfiguration Build();
} 