using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using SaslMechanism = Confluent.Kafka.SaslMechanism;
using SecurityProtocol = Confluent.Kafka.SecurityProtocol;
using MessageHook.Kafka.Extensions;
using MessageHook.Kafka.Serializers;
using MessageHook.Kafka.Configurations;
using MessageHook.Tests.Consts;
using MessageHook.Tests.Factories;
using MessageHook.Tests.Models;
using MessageHook.Orchestration.Configurations;
using MessageHook.Orchestration.Factories;
using KafkaFlow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Steeltoe.Extensions.Configuration.Placeholder;
using Xunit.Abstractions;

namespace MessageHook.Tests;

public class GeneralTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    public IConfigurationRoot Configuration { get; set; }
    private IServiceProvider _provider;

    public GeneralTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        IServiceCollection services = new ServiceCollection();

        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddPlaceholderResolver()
            .Build();

        services.AddSingleton<IConfiguration>(Configuration);
        services.AddLogging(loggingBuilder => { loggingBuilder.AddConsole(); });
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
                        .AddConsumingType(typeof(Animal));
                })
                .AddProducer(producerBuilder =>
                {
                    producerBuilder.AddProducerTopic(TestConsts.PRODUCER_ANIMAL)
                        .AddProducerSerializer(new KafkaUTF8Serializer());
                });
        });
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task MessageHookTest()
    {
        try
        {
            var MessageHookFactory = _provider.GetRequiredService<IMessageHookFactory>();
            var MessageHookStep = await MessageHookFactory.CreateMessageHookStepAsync(new MessageHookConfiguration()
            {
                ProduceTo = TestConsts.PRODUCER_ANIMAL,
                ConsumeFrom = new[] { TestConsts.CONSUMER_TOPIC },
                ConsumingOptions = new ConsumingOptionsConfiguration()
                {
                    TimeOut = TimeSpan.FromSeconds(30),
                    MsgReceivedCount = 1,
                }
            });

            var animal = AnimalFactory.Create();
            var waitForMessagesTask = await MessageHookStep.ExecuteAsync($"{animal.Id}", animal);
            await Task.Delay(1000);

            var messages = await waitForMessagesTask.Task;
            Console.WriteLine(JsonSerializer.Serialize(messages));
        }
        finally
        {
            Console.WriteLine("Clean Up");
            DeleteConsumerGroupAsync().GetAwaiter().GetResult();
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

        if (Enum.TryParse<SecurityProtocol>(kafkaBrokerConfig.Credentials.SecurityProtocol, true, out var securityProtocol))
            adminClientConfig.SecurityProtocol = securityProtocol;

        if (kafkaBrokerConfig.Credentials.HasCredentials)
        {
            adminClientConfig.SaslUsername = kafkaBrokerConfig.Credentials.Username;
            adminClientConfig.SaslPassword = kafkaBrokerConfig.Credentials.Password;

            if (Enum.TryParse<SaslMechanism>(kafkaBrokerConfig.Credentials.Mechanism, true, out var saslMechanism))
                adminClientConfig.SaslMechanism = saslMechanism;
        }

        var adminClient = new AdminClientBuilder(adminClientConfig).Build();

        await adminClient.ListConsumerGroupsAsync(new ListConsumerGroupsOptions
        {
            MatchStates = new[] { ConsumerGroupState.Empty }
        });
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
                Console.WriteLine(e);
            }
        }
    }
}
