# MessageHook.Kafka

Kafka integration for **MessageHook** — a testing framework that lets you produce a Kafka message and wait for the response in a single step, with built-in correlation ID and message key filtering.

## Installation

```bash
dotnet add package MessageHook.Kafka
```

> Installing `MessageHook.Kafka` automatically pulls `MessageHook.Orchestration` and `MessageHook.Core`.

## Setup

Register MessageHook in your test project's dependency-injection setup:

```csharp
services.AddKafkaMessageHook(builder =>
{
    builder.ConfigureBroker(broker =>
        {
            broker.WithBootstrapServers(new[] { "localhost:9092" });
        })
        .AddConsumer(consumer =>
        {
            consumer.AddTopic("response-topic")
                    .AddConsumerGroup("my-test-group")
                    .AddConsumingSerializer(new KafkaUTF8Serializer())
                    .AddConsumingType(typeof(MyResponseType));
        })
        .AddProducer(producer =>
        {
            producer.AddProducerTopic("request-topic")
                    .AddProducerSerializer(new KafkaUTF8Serializer());
        });
});
```

## Usage

### Correlation ID mode (default)

Produce a message and wait for a response that carries the same correlation ID:

```csharp
var factory = provider.GetRequiredService<IMessageHookFactory>();

var step = await factory.CreateMessageHookStepAsync(new MessageHookConfiguration
{
    ProduceTo   = "request-topic",
    ConsumeFrom = new[] { "response-topic" },
    ConsumingOptions = new ConsumingOptionsConfiguration
    {
        TimeOut          = TimeSpan.FromSeconds(30),
        MsgReceivedCount = 1
    }
});

var result = await step.ExecuteAsync("correlation-id", myPayload);
var messages = await result.Task;
```

### Message key mode

Filter consumed messages by a specific Kafka message key:

```csharp
var step = await factory.CreateMessageHookStepAsync(new MessageHookConfiguration
{
    ProduceTo   = "request-topic",
    ConsumeFrom = new[] { "response-topic" },
    ConsumingOptions = new ConsumingOptionsConfiguration
    {
        TimeOut          = TimeSpan.FromSeconds(30),
        MsgReceivedCount = 1,
        ExpectedMessageKey = "my-key"
    }
});

var result = await step.ExecuteAsync("my-key", myPayload);
var messages = await result.Task;
```

### Produce with headers

```csharp
var result = await step.ExecuteAsync("my-key", myPayload, new ProducingExtraData
{
    Headers = new Dictionary<string, string> { { "x-source", "integration-test" } }
});
```

## EchoService

The repo includes `MessageHook.EchoService` — a ready-made Kafka echo service that consumes from topic `A` and re-produces to topic `B`, preserving the original message key and headers. Useful for local integration testing without a real downstream service.

It also does one small piece of stateful work, so tests have something to assert on beyond a round-trip: it
remembers the last `name` it saw for each `id` and stamps an **`IsChanged`** flag onto the echoed payload.

| Case | `IsChanged` |
|---|---|
| first message for an `id` | `false` |
| same `id`, different `name` than last time | `true` |
| same `id`, same `name` | `false` |

`id` and `name` are matched case-insensitively; when a payload has no `id`, the Kafka message key identifies it
instead. An inbound `IsChanged` is ignored and overwritten. Payloads that aren't JSON objects are echoed
byte-for-byte. The history is in memory, so restarting the service clears it.

```
A: { "id": "1", "name": "cat" }   ->   B: { "id": "1", "name": "cat", "IsChanged": false }
A: { "id": "1", "name": "dog" }   ->   B: { "id": "1", "name": "dog", "IsChanged": true  }
```

## Source & Issues

[github.com/Asher-P/messagehook-testkit](https://github.com/Asher-P/messagehook-testkit)
