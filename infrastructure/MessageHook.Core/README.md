# MessageHook.Core

Core abstractions for **MessageHook** — the integration testing framework for Kafka message flows.

> This is a low-level package. Most users should install **MessageHook.Kafka**, which pulls this package transitively.

## What's inside

| Abstraction | Description |
|---|---|
| `IMessagePool` | In-memory store that holds consumed messages until the test reads them |
| `IFilterService` | Matches incoming messages against the expected correlation ID or message key |
| `IConsumer` / `IProducer` | Transport-agnostic interfaces implemented by `MessageHook.Kafka` |
| `ResponseContainer` | Wraps a consumed message with its key, value, and headers |
| `ProducingExtraData` | Optional headers and metadata attached when producing |

## Purpose

`MessageHook.Core` exists to keep the orchestration and transport layers decoupled. If you want to implement a non-Kafka transport (e.g. RabbitMQ, Azure Service Bus), implement `IConsumer` and `IProducer` from this package and plug them in.

## Source & Issues

[github.com/Asher-P/messagehook-testkit](https://github.com/Asher-P/messagehook-testkit)
