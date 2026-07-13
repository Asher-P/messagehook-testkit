using MessageHook.Kafka.Configurations;
using KafkaFlow.Configuration;
using System.Collections.Generic;

namespace MessageHook.Kafka.Builders;

public class KafkaBrokerConfigurationBuilder : IKafkaBrokerConfigurationBuilder
{
    public IEnumerable<string> BootstrapServers { get; private set; } = new List<string>();
    public KafkaCredentialsConfiguration Credentials { get; private set; } = new KafkaCredentialsConfiguration();

    public IKafkaBrokerConfigurationBuilder WithBootstrapServers(IEnumerable<string> bootstrapServers)
    {
        BootstrapServers = bootstrapServers;
        return this;
    }

    public IKafkaBrokerConfigurationBuilder WithBootstrapServer(string bootstrapServer)
    {
        BootstrapServers = new List<string> { bootstrapServer };
        return this;
    }

    public IKafkaBrokerConfigurationBuilder WithCredentials(KafkaCredentialsConfiguration credentials)
    {
        Credentials = credentials;
        return this;
    }

    public IKafkaBrokerConfigurationBuilder WithCredentials(string username, string password, string mechanism = "plain", string securityProtocol = "sasl_plaintext", bool tlsEnabled = false)
    {
        Credentials = new KafkaCredentialsConfiguration
        {
            Username = username,
            Password = password,
            Mechanism = mechanism,
            SecurityProtocol = securityProtocol,
            TlsEnabled = tlsEnabled
        };
        return this;
    }

    public KafkaBrokerConfiguration Build()
    {
        return new KafkaBrokerConfiguration
        {
            BootstrapServers = BootstrapServers,
            Credentials = Credentials
        };
    }
} 