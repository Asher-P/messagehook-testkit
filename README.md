# messagehook

Infrastructure for integration tests as a reusable NuGet package.

## Overview

This package provides a robust infrastructure for integration and automation testing, including Kafka consumer/producer setup, dependency injection, and configuration management. It is designed to be consumed as a NuGet package in other projects, enabling rapid and consistent test environment setup.

## Installation

1. Add the following to your `nuget.config` to ensure access to the required feeds:

```xml
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

2. Install the package in your test project:

```sh
dotnet add package MessageHook.Kafka
```

## Configuration

### appsettings.json Structure

Create an `appsettings.json` file in your test project with the following structure:

```json
{
  "KafkaBrokerConfiguration": {
    "BootstrapServers": ["${KAFKA_BOOTSTRAP}"],
    "Credentials": {
      "SecurityProtocol": "SaslSsl",
      "Mechanism": "Plain",
      "Username": "${KAFKA_SASL_USERNAME}",
      "Password": "${KAFKA_SASL_PASSWORD}"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### Environment Variables

Set the following environment variables or use placeholder resolution:

```bash
export KAFKA_BOOTSTRAP="your-kafka-bootstrap-servers"
export KAFKA_SASL_USERNAME="your-username"
export KAFKA_SASL_PASSWORD="your-password"
```

## Basic Usage Example

Here's a complete working example based on real test implementation:

```csharp
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using MessageHook.Core.Messaging.FilterService;
using MessageHook.Core.Messaging.Receivers;
using MessageHook.Kafka.Extensions;
using MessageHook.Kafka.Serializers;
using MessageHook.Kafka.Configurations;
using MessageHook.Orchestration.Configurations;
using MessageHook.Orchestration.Factories;
using KafkaFlow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Steeltoe.Extensions.Configuration.Placeholder;

public class GeneralTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    public IConfigurationRoot Configuration { get; set; }
    private IServiceProvider _provider;

    public GeneralTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        SetUp();
    }

    public void SetUp()
    {
        IServiceCollection services = new ServiceCollection();

        // Configure settings with placeholder resolution
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddPlaceholderResolver()
            .Build();


        // Register core services
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddSingleton<YourFactory>(); // Replace with your factories
        
        // Add logging
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole();
        });

        // Register MessageHook services
        services.AddSingleton<IFilterService, FilterService>();
        services.AddSingleton<IMessagePool, MessagePool>();

        // Configure Kafka MessageHook
        services.AddKafkaMessageHook(builder =>
        {
            // Get configuration from appsettings.json
            var kafkaBrokerConfig = Configuration.GetSection("KafkaBrokerConfiguration").Get<KafkaBrokerConfiguration>();
                
            builder.ConfigureBroker(brokerBuilder =>
                {
                    brokerBuilder.WithBootstrapServers(kafkaBrokerConfig.BootstrapServers)
                        .WithCredentials(kafkaBrokerConfig.Credentials);
                })
                .AddConsumer(consumerBuilder =>
                {
                    consumerBuilder.AddTopic("YOUR_CONSUMER_TOPIC")
                        .AddConsumerGroup("YOUR_CONSUMER_GROUP")
                        .AddConsumingSerializer(new KafkaUTF8Serializer())
                        .AddConsumingType(typeof(YourMessageType));
                })
                .AddProducer(producerBuilder =>
                {
                    producerBuilder.AddProducerTopic("YOUR_PRODUCER_TOPIC")
                        .AddProducerSerializer(new KafkaProtobufSerializer());
                });
        });

        _provider = services.BuildServiceProvider();
    }
}
```

## Complete Test Examples

### 1. Producer-Driven Flow (Standard MessageHook)

Here's a test where you produce a message and wait for the response:

```csharp
[Fact]
public async Task MessageHookTest_ProducerDriven()
{
    try
    {
        // Setup MessageHook - NO ExpectedMessageKey needed (MessageHook creates correlationId)
        var MessageHookFactory = _provider.GetRequiredService<IMessageHookFactory>();
        var MessageHookStep = await MessageHookFactory.CreateMessageHookStepAsync(new MessageHookConfiguration()
        {
            ProduceTo = "YOUR_PRODUCER_TOPIC",
            ConsumeFrom = new[] { "YOUR_CONSUMER_TOPIC" },
            ConsumingOptions = new ConsumingOptionsConfiguration()
            {
                TimeOut = TimeSpan.FromSeconds(30),
                MsgReceivedCount = 1,
                // ❌ DO NOT set ExpectedMessageKey when producing
            }
        });

        // Create your test data
        var testMessage = CreateYourTestMessage();

        // Send Message - MessageHook framework creates correlationId automatically
        var waitForMessagesTask = await MessageHookStep.ExecuteAsync(
            "your-message-key", testMessage);
        
        // Allow processing time
        await Task.Delay(1000);

        // Receive and validate messages
        var messages = await waitForMessagesTask.Task;
        
        // Validate results
        Assert.NotNull(messages);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(messages));
    }
    finally
    {
        // Clean up consumer groups
        await DeleteConsumerGroupAsync();
    }
}
```

### 2. MessageKey Flow (Consume by Key)

Use this when you want to consume messages matched by their Kafka message key instead of a correlationId. This applies to any flow where there is no `correlationId` — external triggers, database changes, broadcasts, or any system that produces messages with a known, predictable key.

```csharp
[Fact]
public async Task MessageHookTest_MessageKeyFlow()
{
    try
    {
        var objectId = 12345;
        
        // Setup MessageHook - set ExpectedMessageKey to filter by Kafka message key
        var MessageHookFactory = _provider.GetRequiredService<IMessageHookFactory>();
        var MessageHookStep = await MessageHookFactory.CreateMessageHookStepAsync(new MessageHookConfiguration()
        {
            // No ProduceTo - consuming only, matched by message key
            ConsumeFrom = new[] { "YourService.Entity.Validated" },
            ConsumingOptions = new ConsumingOptionsConfiguration()
            {
                ExpectedMessageKey = $"{objectId}", // the Kafka message key to match on
                TimeOut = TimeSpan.FromSeconds(30),
                MsgReceivedCount = 1,
            }
        });

        // Trigger the action that will produce the message
        await TriggerDatabaseChange(objectId);

        // Wait for message matching the expected key - no parameters needed
        var waitForMessagesTask = await MessageHookStep.ExecuteAsync();
        
        // Allow processing time
        await Task.Delay(2000);

        // Receive and validate messages
        var messages = await waitForMessagesTask.Task;
        
        // Validate results
        Assert.NotNull(messages);
        Console.WriteLine($"Received message: {messages.JsonSerialize()}");
    }
    finally
    {
        await DeleteConsumerGroupAsync();
    }
}

private async Task TriggerDatabaseChange(int objectId)
{
    using var dbContext = _provider.GetRequiredService<YourDbContext>();
    var entity = await dbContext.Entities.FindAsync(objectId);
    if (entity != null)
    {
        entity.LastUpdate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
    }
}
```

### 3. Multi-Message MessageKey Flow

For scenarios expecting multiple messages matched by key:

```csharp
[Fact]
public async Task MessageHookTest_MultipleMessages()
{
    try
    {
        var objectId = 12345;

        var MessageHookStep = await MessageHookFactory.CreateMessageHookStepAsync(new MessageHookConfiguration()
        {
            ConsumeFrom = new[] { 
                "YourService.Entity.Validated",
                "YourService.Entity.Invalid" 
            },
            ConsumingOptions = new ConsumingOptionsConfiguration()
            {
                ExpectedMessageKey = $"{objectId}", // the Kafka message key to match on
                TimeOut = TimeSpan.FromSeconds(45),
                MsgReceivedCount = 2, // Expecting messages in multiple topics
            }
        });

        // Trigger database changes that affect multiple entities
        await TriggerMultipleEntityChanges();

        var waitForMessagesTask = await MessageHookStep.ExecuteAsync();
        await Task.Delay(3000);

        var messages = await waitForMessagesTask.Task;
        
        // Validate we received messages from multiple topics
        Assert.True(messages.Count() >= 2);
    }
    finally
    {
        await DeleteConsumerGroupAsync();
    }
}
```

## Consumer Group Cleanup

Proper cleanup is essential to avoid test interference. Here's the cleanup implementation:

```csharp
public async Task DeleteConsumerGroupAsync()
{
    var kafkaBus = _provider.GetRequiredService<IKafkaBus>();
    
    // Stop all consumers
    foreach (var messageConsumer in kafkaBus.Consumers.All)
    {
        await messageConsumer.StopAsync();
    }

    Thread.Sleep(TimeSpan.FromSeconds(10));
    
    // Get configuration for admin client
    var kafkaBrokerConfig = Configuration.GetSection("KafkaBrokerConfiguration").Get<KafkaBrokerConfiguration>();
    
    // Configure admin client with security settings
    var adminClientConfig = new AdminClientConfig()
    {
        BootstrapServers = string.Join(",", kafkaBrokerConfig.BootstrapServers),
    };
    
    // Set security protocol
    if (Enum.TryParse<SecurityProtocol>(kafkaBrokerConfig.Credentials.SecurityProtocol, true, out var securityProtocol))
    {
        adminClientConfig.SecurityProtocol = securityProtocol;
    }
    
    // Set SASL credentials if available
    if (kafkaBrokerConfig.Credentials.HasCredentials)
    {
        adminClientConfig.SaslUsername = kafkaBrokerConfig.Credentials.Username;
        adminClientConfig.SaslPassword = kafkaBrokerConfig.Credentials.Password;
        
        if (Enum.TryParse<SaslMechanism>(kafkaBrokerConfig.Credentials.Mechanism, true, out var saslMechanism))
        {
            adminClientConfig.SaslMechanism = saslMechanism;
        }
    }
    
    var adminClient = new AdminClientBuilder(adminClientConfig).Build();

    await kafkaBus.StopAsync();
    while (true)
    {
        try
        {
            await adminClient.DeleteGroupsAsync(new List<string>()
            {
                "YOUR_CONSUMER_GROUP",
            });
            break;
        }
        catch (DeleteGroupsException e)
        {
            if (e.Message.Contains("The group id does not exist"))
                return;
            Console.WriteLine(e);
            await Task.Delay(1000);
        }
    }
}
```

## Configuration Patterns

### Multiple Consumers and Producers

```csharp
services.AddKafkaMessageHook(builder =>
{
    var kafkaBrokerConfig = Configuration.GetSection("KafkaBrokerConfiguration").Get<KafkaBrokerConfiguration>();
        
    builder.ConfigureBroker(brokerBuilder =>
        {
            brokerBuilder.WithBootstrapServers(kafkaBrokerConfig.BootstrapServers)
                .WithCredentials(kafkaBrokerConfig.Credentials);
        })
        // Multiple consumers
        .AddConsumer(consumerBuilder =>
        {
            consumerBuilder.AddTopic("TOPIC_A")
                .AddConsumerGroup("GROUP_A")
                .AddConsumingSerializer(new KafkaUTF8Serializer())
                .AddConsumingType(typeof(MessageTypeA));
        })
        .AddConsumer(consumerBuilder =>
        {
            consumerBuilder.AddTopic("TOPIC_B")
                .AddConsumerGroup("GROUP_B")
                .AddConsumingSerializer(new KafkaProtobufSerializer())
                .AddConsumingType(typeof(MessageTypeB));
        })
        // Multiple producers
        .AddProducer(producerBuilder =>
        {
            producerBuilder.AddProducerTopic("OUTPUT_TOPIC_A")
                .AddProducerSerializer(new KafkaUTF8Serializer());
        })
        .AddProducer(producerBuilder =>
        {
            producerBuilder.AddProducerTopic("OUTPUT_TOPIC_B")
                .AddProducerSerializer(new KafkaProtobufSerializer());
        });
});
```

### Custom Serializers

```csharp
// UTF-8 String serializer for JSON messages
.AddConsumingSerializer(new KafkaUTF8Serializer())

// Protobuf serializer for binary messages
.AddConsumingSerializer(new KafkaProtobufSerializer())
```

## ExpectedMessageKey Usage Patterns

### When to Use ExpectedMessageKey

| Scenario | Use ExpectedMessageKey? | Reason |
|----------|-------------------------|---------|
| **Producing Messages** | ❌ **NO** | MessageHook injects `correlationId` automatically |
| **External Events / Key-Based Flows** | ✅ **YES** | No `correlationId` — filter by the known Kafka message key instead |
| **Database Changes** | ✅ **YES** | No `correlationId` — use the message key to identify the expected event |
| **External API Triggers** | ✅ **YES** | External systems don't provide correlation IDs |

### ExpectedMessageKey Format Examples

```csharp
// Composite key: ParentId_ChildId
ExpectedMessageKey = {Message Key}

// Provider data: ProviderId_DataType_ProviderRecordId
ExpectedMessageKey = $"{providerId}_DataType_{providerRecordId}"

// Multi-part keys: Component1_Component2_Component3
ExpectedMessageKey = $"{parentId}_{providerId}_{childType}_{childId}"
```

### Key Rules Summary

```csharp
// ✅ CORRECT - Producing messages (NO ExpectedMessageKey)
var MessageHookStep = await MessageHookFactory.CreateMessageHookStepAsync(new MessageHookConfiguration()
{
    ProduceTo = "OUTPUT_TOPIC",
    ConsumeFrom = new[] { "INPUT_TOPIC" },
    ConsumingOptions = new ConsumingOptionsConfiguration()
    {
        // ❌ DO NOT set ExpectedMessageKey when producing
        TimeOut = TimeSpan.FromSeconds(30),
        MsgReceivedCount = 1,
    }
});

// Execute with message - MessageHook creates correlationId
await MessageHookStep.ExecuteAsync(messageKey, message);
```

```csharp
// ✅ CORRECT - MessageKey flows (consume by key, no correlationId)
var MessageHookStep = await MessageHookFactory.CreateMessageHookStepAsync(new MessageHookConfiguration()
{
    ConsumeFrom = new[] { "INPUT_TOPIC" },
    ConsumingOptions = new ConsumingOptionsConfiguration()
    {
        ExpectedMessageKey = {Message Key}, // filter by Kafka message key
        TimeOut = TimeSpan.FromSeconds(30),
        MsgReceivedCount = 1,
    }
});

// Execute without parameters - waiting for message matching the expected key
await MessageHookStep.ExecuteAsync();
```

## Best Practices

### 1. **Always Use Try-Finally for Cleanup**
```csharp
[Fact]
public async Task YourTest()
{
    try
    {
        // Test logic here
    }
    finally
    {
        await DeleteConsumerGroupAsync();
    }
}
```

### 2. **Set Appropriate Timeouts**
```csharp
ConsumingOptions = new ConsumingOptionsConfiguration()
{
    TimeOut = TimeSpan.FromSeconds(30), // Adjust based on your needs
    MsgReceivedCount = 1, // Expected number of messages
}
```

### 3. **Use Meaningful Message Keys**
```csharp
var messageKey = {Message Key}; // Use business identifiers
await MessageHookStep.ExecuteAsync(messageKey, testMessage);
```

### 4. **Add Processing Delays**
```csharp
var waitForMessagesTask = await MessageHookStep.ExecuteAsync(key, message);
await Task.Delay(1000); // Allow message processing time
var messages = await waitForMessagesTask.Task;
```

## Troubleshooting

### Common Issues

1. **Consumer Group Still Active**: Ensure proper cleanup in finally blocks
2. **Timeout Issues**: Increase timeout values or check message processing logic
3. **Serialization Errors**: Verify message types match serializer expectations
4. **Connection Issues**: Check Kafka configuration and credentials

### Debug Output
```csharp
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(messages)); // Log received messages
```

## Dependencies

Required NuGet packages:
- `MessageHook.Core`
- `MessageHook.Kafka`
- `KafkaFlow`
- `Confluent.Kafka`

## Notes

- This package is intended for use as a test infrastructure dependency in other projects
- All configuration values should be provided via your own appsettings or environment variables
- No secrets or credentials are included in this package
- Always clean up consumer groups to prevent test interference
- Use placeholder resolution for environment-specific configuration
