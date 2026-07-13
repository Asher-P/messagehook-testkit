using MessageHook.Kafka.Configurations;
using MessageHook.Kafka.Middlewares;
using KafkaFlow;
using Microsoft.Extensions.DependencyInjection;
using AutoOffsetReset = KafkaFlow.AutoOffsetReset;
using SaslMechanism = KafkaFlow.Configuration.SaslMechanism;
using SecurityProtocol = KafkaFlow.Configuration.SecurityProtocol;

namespace MessageHook.Kafka.Builders;

public class KafkaConfigurationBuilder : IKafkaConfigurationBuilder
{
    private readonly IServiceCollection _serviceCollection;
    public KafkaBrokerConfiguration BrokerConfiguration { get; private set; } = new KafkaBrokerConfiguration();

    public IEnumerable<KafkaConsumerConfiguration> KafkaConsumers { get; set; } =
        new List<KafkaConsumerConfiguration>();

    public IEnumerable<KafkaProducerConfiguration> KafkaProducers { get; set; } =
        new List<KafkaProducerConfiguration>();

    public IKafkaConfigurationBuilder SetBrokerConfiguration(KafkaBrokerConfiguration brokerConfiguration)
    {
        BrokerConfiguration = brokerConfiguration;
        return this;
    }

    public IKafkaConfigurationBuilder ConfigureBroker(Action<IKafkaBrokerConfigurationBuilder> brokerConfigurationBuilder)
    {
        IKafkaBrokerConfigurationBuilder builder = new KafkaBrokerConfigurationBuilder();
        brokerConfigurationBuilder.Invoke(builder);
        BrokerConfiguration = builder.Build();
        return this;
    }

    public IKafkaConfigurationBuilder AddProducer(Action<IKafkaProducerConfigurationBuilder> producerBuilder)
    {
        IKafkaProducerConfigurationBuilder builder = new KafkaProducerConfigurationBuilder();
        producerBuilder.Invoke(builder);
        KafkaProducers = KafkaProducers.Append(builder.Build());
        return this;
    }

    public IKafkaConfigurationBuilder AddConsumer(Action<IKafkaConsumerConfigurationBuilder> consumerBuilder)
    {
        IKafkaConsumerConfigurationBuilder builder = new KafkaConsumerConfigurationBuilder();
        consumerBuilder.Invoke(builder);
        KafkaConsumers = KafkaConsumers.Append(builder.Build());
        return this;
    }


    public IServiceCollection BuildKafkaFlow(IServiceCollection _serviceCollection)
    {
        return _serviceCollection.AddKafkaFlowHostedService(builder =>
            {
                builder
                    .AddCluster(cluster =>
                    {
                        cluster.WithBrokers(BrokerConfiguration.BootstrapServers);
                        
                        // Configure security settings based on provided credentials
                        if (BrokerConfiguration.Credentials.HasCredentials)
                        {
                            cluster.WithSecurityInformation(information =>
                            {
                                information.SecurityProtocol = ParseSecurityProtocol(BrokerConfiguration.Credentials.SecurityProtocol);
                                
                                if (BrokerConfiguration.Credentials.TlsEnabled)
                                {
                                    information.SslCaLocation = string.Empty; // Can be configured if needed
                                }
                                
                                information.SaslUsername = BrokerConfiguration.Credentials.Username;
                                information.SaslPassword = BrokerConfiguration.Credentials.Password;
                                information.SaslMechanism = ParseSaslMechanism(BrokerConfiguration.Credentials.Mechanism);
                            });
                        }
                        else
                        {
                            cluster.WithSecurityInformation(information =>
                                information.SecurityProtocol = SecurityProtocol.Plaintext);
                        }
                        
                        foreach (var kafkaConsumerConfiguration in KafkaConsumers)
                        {
                            cluster.AddConsumer(consumer =>
                            {
                                consumer.WithName(kafkaConsumerConfiguration.Topic);
                                consumer.Topic(kafkaConsumerConfiguration.Topic);
                                consumer.WithAutoOffsetReset(AutoOffsetReset.Latest);
                                consumer.WithGroupId(kafkaConsumerConfiguration.ConsumerGroup);
                                consumer.WithWorkersCount(kafkaConsumerConfiguration.WorkersCount?? 50);
                                consumer.WithBufferSize(kafkaConsumerConfiguration.BufferSize?? 30);
                                consumer.AddMiddlewares(middlewares =>
                                {
                                    middlewares.Add<ErrorHandlingMiddleware>()
                                        .Add<FilterMiddleware>(MiddlewareLifetime.Singleton)
                                        .AddSingleTypeDeserializer(resolver => kafkaConsumerConfiguration.ConsumerDeserialization,
                                        kafkaConsumerConfiguration.ConsumingType)
                                        .Add<PushMessageMiddleware>();
                                });
                            });
                        }

                        foreach (var producerConfiguration in KafkaProducers)
                        {
                            cluster.AddProducer(producerConfiguration.ProducerTopic,producer =>
                            {
                                producer.DefaultTopic(producerConfiguration.ProducerTopic)
                                    .AddMiddlewares(middlewares =>
                                    {
                                        middlewares.AddSerializer(resolver => producerConfiguration.ProducerSerializer);
                                    });
                            });
                        }
                    });
            })
            .AddSingleton<IKafkaBus>(resolve => resolve.CreateKafkaBus());
    }
    
    private SecurityProtocol ParseSecurityProtocol(string securityProtocol)
    {
        return securityProtocol?.ToLower() switch
        {
            "saslssl" or "sasl_ssl" => SecurityProtocol.SaslSsl,
            "ssl" => SecurityProtocol.Ssl,
            "saslplaintext" or "sasl_plaintext" => SecurityProtocol.SaslPlaintext,
            _ => SecurityProtocol.Plaintext
        };
    }
    
    private SaslMechanism ParseSaslMechanism(string mechanism)
    {
        return mechanism?.ToLower() switch
        {
            "plain" => SaslMechanism.Plain,
            "scram-sha-256" or "scram_sha_256" => SaslMechanism.ScramSha256,
            "scram-sha-512" or "scram_sha_512" => SaslMechanism.ScramSha512,
            "gssapi" => SaslMechanism.Gssapi,
            _ => SaslMechanism.Plain
        };
    }
}