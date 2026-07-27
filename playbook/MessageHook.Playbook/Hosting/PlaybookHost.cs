using MessageHook.Kafka.Configurations;
using MessageHook.Kafka.Extensions;
using MessageHook.Playbook.Loading;
using MessageHook.Playbook.Models;
using MessageHook.Playbook.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MessageHook.Playbook.Hosting;

/// <summary>
/// Translates a <see cref="PlaybookDefinition"/> into the existing <c>AddKafkaMessageHook</c> registration and
/// owns the resulting <see cref="IServiceProvider"/>. Each host uses its own per-run consumer group and, on
/// disposal, stops the bus, deletes that group, and disposes the provider — so a long-lived process can build
/// and tear down many hosts without leaking consumers.
/// </summary>
public sealed class PlaybookHost : IAsyncDisposable
{
    /// <summary>
    /// Every host's service provider is rooted here for the life of the process and never disposed or stopped.
    /// Both stopping and disposing a KafkaFlow consumer let its background poll loop resume one last
    /// <c>rd_kafka_consumer_poll</c> from a cancellation continuation after the native handle is gone — a
    /// use-after-free that raises an access violation and hard-kills the whole process (it cannot be caught).
    /// So a host, once built, keeps running until the process exits. Rooting the provider here also keeps the GC
    /// from finalizing — and thus freeing — it on a finalizer thread. <see cref="PlaybookRunner"/> caches one
    /// host per Kafka config and reuses it across runs, so this set holds one entry per distinct config, not one
    /// per run (mirroring how the hand-written NUnit tests reuse a single provider for many runs).
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentBag<IServiceProvider> RetainedProviders = new();

    private readonly KafkaBrokerConfiguration _broker;

    public IServiceProvider Services { get; }

    /// <summary>The consumer group actually used (base group + short suffix), stable for this host's lifetime.</summary>
    public string ConsumerGroup { get; }

    public PlaybookHost(
        PlaybookDefinition definition,
        PlaceholderExpander expander,
        SerializerRegistry serializers,
        string? consumerGroupOverride,
        ILoggerFactory? loggerFactory)
    {
        _broker = BuildBroker(definition, expander);

        var baseGroup = consumerGroupOverride
                        ?? (string.IsNullOrWhiteSpace(definition.KafkaConfiguration.ConsumerGroup)
                            ? "messagehook-playbook"
                            : definition.KafkaConfiguration.ConsumerGroup);
        ConsumerGroup = $"{baseGroup}-{Guid.NewGuid():N}"[..(baseGroup.Length + 9)];

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (loggerFactory is null) builder.AddConsole();
        });
        if (loggerFactory is not null)
            services.AddSingleton(loggerFactory);

        services.AddKafkaMessageHook(builder =>
        {
            builder.ConfigureBroker(brokerBuilder =>
                brokerBuilder.WithBootstrapServers(_broker.BootstrapServers)
                             .WithCredentials(_broker.Credentials));

            foreach (var consumer in definition.ConsumeTopics)
            {
                builder.AddConsumer(consumerBuilder =>
                {
                    consumerBuilder.AddTopic(consumer.Topic)
                        .AddConsumerGroup(ConsumerGroup)
                        .AddConsumingSerializer(serializers.GetConsumerDeserializer(consumer.Serializer))
                        .AddConsumingType(serializers.ResolveType(consumer.MessageType));
                    if (consumer.WorkersCount is { } workers) consumerBuilder.SetWorkersCount(workers);
                    if (consumer.BufferSize is { } buffer) consumerBuilder.SetBufferSize(buffer);
                });
            }

            foreach (var producer in definition.ProduceTopics)
            {
                builder.AddProducer(producerBuilder =>
                    producerBuilder.AddProducerTopic(producer.Topic)
                                   .AddProducerSerializer(serializers.GetProducerSerializer(producer.Serializer)));
            }
        });

        Services = services.BuildServiceProvider();

        // Root the provider for the process lifetime so it is never finalized (and its native consumer never
        // freed) — see RetainedProviders. The runner caches hosts, so this is one entry per Kafka config.
        RetainedProviders.Add(Services);
    }

    private static KafkaBrokerConfiguration BuildBroker(PlaybookDefinition definition, PlaceholderExpander expander)
    {
        var empty = new Dictionary<string, string>();
        string? Expand(string? value) => value is null ? null : expander.ExpandString(value, empty);

        var creds = definition.KafkaConfiguration.Credentials;
        return new KafkaBrokerConfiguration
        {
            BootstrapServers = definition.KafkaConfiguration.BootstrapServers
                .Select(s => expander.ExpandString(s, empty)).ToList(),
            Credentials = new KafkaCredentialsConfiguration
            {
                Username = Expand(creds.Username),
                Password = Expand(creds.Password),
                Mechanism = creds.Mechanism,
                SecurityProtocol = creds.SecurityProtocol,
                TlsEnabled = creds.TlsEnabled
            }
        };
    }

    /// <summary>
    /// Intentionally a no-op. A host is never stopped or disposed — doing either crashes the process (see
    /// <see cref="RetainedProviders"/>). Hosts are cached and reused by <see cref="PlaybookRunner"/> and live
    /// until the process exits, when the OS reclaims their sockets and native handles.
    /// </summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
