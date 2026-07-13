using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MessageHook.Core.Messaging.Publishing.Entities;
using MessageHook.Kafka.Extensions;
using MessageHook.Kafka.Serializers;
using MessageHook.Kafka.Configurations;
using MessageHook.NUnit.Consts;
using MessageHook.NUnit.Factories;
using MessageHook.Tests.Models;
using MessageHook.Orchestration.Configurations;
using MessageHook.Orchestration.Factories;
using MessageHook.Orchestration.Entities.Interfaces;
using KafkaFlow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Steeltoe.Extensions.Configuration.Placeholder;

namespace MessageHook.NUnit;

public class MessageKeyMessageHookTest
{
    public IConfigurationRoot Configuration { get; set; }
    private IServiceProvider _provider;

    [SetUp]
    public void SetUp()
    {
        IServiceCollection services = new ServiceCollection();

        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddPlaceholderResolver()
            .Build();

        services.AddLogging(loggingBuilder => { loggingBuilder.AddConsole(); });
        services.AddSingleton<IConfiguration>(x => Configuration);

        services.AddKafkaMessageHook(builder =>
        {
            var kafkaBrokerConfig = Configuration.GetSection("KafkaBrokerConfiguration").Get<KafkaBrokerConfiguration>();

            builder.ConfigureBroker(brokerBuilder =>
                {
                    brokerBuilder.WithBootstrapServers(kafkaBrokerConfig.BootstrapServers)
                        .WithCredentials(kafkaBrokerConfig.Credentials);
                })
                .AddConsumer(consumerBuilder =>
                {
                    consumerBuilder.AddTopic(TestConsts.CONSUMER_TOPIC)
                        .AddConsumerGroup(TestConsts.CONSUMER_GROUP)
                        .AddConsumingSerializer(new KafkaUTF8Serializer())
                        .AddConsumingType(typeof(Animal))
                        .SetWorkersCount(55)
                        .SetBufferSize(50);
                })
                .AddProducer(producerBuilder =>
                {
                    producerBuilder.AddProducerTopic(TestConsts.PRODUCER_ANIMAL)
                        .AddProducerSerializer(new KafkaUTF8Serializer());
                });
        });
        _provider = services.BuildServiceProvider();
    }

    [Test]
    public async Task MessageKeyMessageHookTest_ProduceWithKeyAndConsumeByKey()
    {
        try
        {
            var animal = AnimalFactory.Create("Luna");

            var MessageHookFactory = _provider.GetRequiredService<IMessageHookFactory>();
            var MessageHookStep = await MessageHookFactory.CreateMessageHookStepAsync(new MessageHookConfiguration()
            {
                ProduceTo = TestConsts.PRODUCER_ANIMAL,
                ConsumeFrom = new[] { TestConsts.CONSUMER_TOPIC },
                ConsumingOptions = new ConsumingOptionsConfiguration()
                {
                    TimeOut = TimeSpan.FromSeconds(30),
                    MsgReceivedCount = 1,
                    ExpectedMessageKey = $"{animal.Id}"
                }
            });

            var waitForMessagesTask = await MessageHookStep.ExecuteAsync($"{animal.Id}", animal);
            await Task.Delay(1000);

            var messages = await waitForMessagesTask.Task;
            Console.WriteLine($"Received {messages.Count()} message containers");
            Console.WriteLine(JsonSerializer.Serialize(messages));
        }
        finally
        {
            Console.WriteLine("Clean Up");
            await DeleteConsumerGroupAsync();
        }
    }

    [Ignore("missing information")]
    [Test]
    public async Task MessageKeyMessageHookTest_ConsumeOnlyMode()
    {
        try
        {
            var MessageHookFactory = _provider.GetRequiredService<IMessageHookFactory>();
            var MessageHookStep = await MessageHookFactory.CreateMessageHookStepAsync(new MessageHookConfiguration()
            {
                ConsumeFrom = new[] { TestConsts.CONSUMER_TOPIC },
                ConsumingOptions = new ConsumingOptionsConfiguration()
                {
                    TimeOut = TimeSpan.FromSeconds(30),
                    MsgReceivedCount = 1,
                    ExpectedMessageKey = "specific-consume-key"
                }
            });

            await MessageHookStep.ExecuteAsync<string>("", "");
            await Task.Delay(1000);

            Console.WriteLine("MessageHook set up to consume only messages with key: specific-consume-key");
        }
        catch (TimeoutException)
        {
            Console.WriteLine("No message with expected key received - this is expected for this demo");
        }
        finally
        {
            Console.WriteLine("Clean Up");
            await DeleteConsumerGroupAsync();
        }
    }

    public async Task DeleteConsumerGroupAsync()
    {
        var kafkaBus = _provider.GetRequiredService<IKafkaBus>();
        foreach (var messageConsumer in kafkaBus.Consumers.All)
            await messageConsumer.StopAsync();

        Thread.Sleep(TimeSpan.FromSeconds(10));

        var kafkaBrokerConfig = Configuration.GetSection("KafkaBrokerConfiguration").Get<KafkaBrokerConfiguration>();

        var adminClientConfig = new AdminClientConfig
        {
            BootstrapServers = string.Join(",", kafkaBrokerConfig.BootstrapServers),
        };

        if (Enum.TryParse<Confluent.Kafka.SecurityProtocol>(kafkaBrokerConfig.Credentials.SecurityProtocol, true, out var securityProtocol))
            adminClientConfig.SecurityProtocol = securityProtocol;

        if (kafkaBrokerConfig.Credentials.HasCredentials)
        {
            adminClientConfig.SaslUsername = kafkaBrokerConfig.Credentials.Username;
            adminClientConfig.SaslPassword = kafkaBrokerConfig.Credentials.Password;

            if (Enum.TryParse<Confluent.Kafka.SaslMechanism>(kafkaBrokerConfig.Credentials.Mechanism, true, out var saslMechanism))
                adminClientConfig.SaslMechanism = saslMechanism;
        }

        var adminClient = new AdminClientBuilder(adminClientConfig).Build();

        await kafkaBus.StopAsync();
        while (true)
        {
            try
            {
                await kafkaBus.StopAsync();
                await adminClient.DeleteGroupsAsync(new List<string> { TestConsts.CONSUMER_GROUP });
                break;
            }
            catch (DeleteGroupsException e)
            {
                if (e.Message.Contains("The group id does not exist"))
                    return;
                Console.WriteLine(e);
            }
        }
    }
}
