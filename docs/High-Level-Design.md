# Automation Playbook Framework - High Level Design (HLD)

## 1. Executive Summary

The **Automation Playbook** is a robust, reusable .NET infrastructure library designed to standardize and simplify integration and automation testing for microservices. It abstracts the complexities of Kafka messaging, dependency injection, and test orchestration, allowing QA and developers to focus on writing test logic rather than maintaining boilerplate infrastructure.

## 2. Architecture Overview

The framework operates as a middleware layer between the Test Runner (e.g., xUnit/NUnit) and the System Under Test (SUT) infrastructure (primarily Kafka).

```mermaid
graph TD
    subgraph "Test Environment"
        TR[Test Runner / Test Project]
        subgraph "Automation Playbook Framework"
            Orch[MessageHook.Orchestration]
            Core[MessageHook.Core]
            KafkaLib[MessageHook.Kafka]
        end
    end

    subgraph "Infrastructure"
        KB[Kafka Broker]
    end

    subgraph "System Under Test"
        SUT[Microservice]
    end

    TR -->|Configures| Orch
    TR -->|Executes Steps| Orch
    Orch -->|Uses| Core
    Orch -->|Delegates to| KafkaLib
    KafkaLib -->|Produces/Consumes| KB
    SUT -->|Consumes/Produces| KB
```

## 3. Core Components

The solution is divided into three main logical layers:

### 3.1 MessageHook.Core (Abstractions)
The foundational layer defining interfaces and domain models.
*   **Responsibilities**: Defines `IMessagePool`, `IFilterService`, and core Messaging interfaces.
*   **Key Abstractions**: Interface definitions for Receivers, Publishers, and Filtering logic independent of the underlying transport (Kafka).

### 3.2 MessageHook.Kafka (Implementation)
The concrete implementation layer specifically for Apache Kafka.
*   **Responsibilities**: Wraps `KafkaFlow` and `Confluent.Kafka` to provide easy-to-use builders and clients.
*   **Key Features**:
    *   **Builders**: `KafkaBrokerConfigurationBuilder`, `KafkaConsumerConfigurationBuilder` for fluent setup.
    *   **Serializers**: Support for JSON (`KafkaUTF8Serializer`) and Protobuf (`KafkaProtobufSerializer`).
    *   **Middleware**: Custom error handling and message filtering middleware.

### 3.3 MessageHook.Orchestration (Flow Control)
The "brain" of the framework that manages the test lifecycle.
*   **Responsibilities**: Synchronizes the asynchronous nature of messaging with the synchronous nature of tests.
*   **Key Components**:
    *   **`IMessageHookFactory`**: Creates initialized `MessageHookStep` instances.
    *   **`MessageHookStep`**: Represents a single test action (Produce -> Wait -> Consume).
    *   **`MessagePool`**: A thread-safe collection holding consumed messages until requested by the test.

## 4. Key Design Patterns

*   **Builder Pattern**: Used extensively for configuration (e.g., `.AddKafkaMessageHook(builder => ...)`), allowing fluent and readable setup of complex Kafka topologies.
*   **Factory Pattern**: `MessageHookFactory` abstracting the creation of MessageHook steps, ensuring all dependencies (like `IMessagePool`) are correctly injected.
*   **Strategy Pattern**: Used for Serialization (switching between UTF8/Protobuf) and Correlation (switching between Auto-CorrelationId and ExpectedMessageKey).

## 5. Operational Workflows

### 5.1 Producer-Driven Flow (Active)
Used when the test initiates the action by sending a command message.

```mermaid
sequenceDiagram
    participant Test as Test Method
    participant JS as MessageHookStep
    participant Kafka as Kafka
    participant SUT as System Under Test

    Test->>JS: ExecuteAsync(payload)
    Note over JS: Generates CorrelationId
    JS->>Kafka: Produce Message (Header: CorrelationId)
    Kafka->>SUT: Consume
    SUT->>SUT: Process
    SUT->>Kafka: Produce Result (Header: CorrelationId)
    Kafka->>JS: Consume
    Note over JS: Match CorrelationId in MessagePool
    JS->>Test: Return Matched Message
```

### 5.2 MessageKey Flow (Passive — Filter by Key)
Used when consuming messages by their Kafka message key instead of a correlationId. The trigger can be anything — a DB update, an external API call, a broadcast, or any event that produces a Kafka message with a known, predictable key.

```mermaid
sequenceDiagram
    participant Test as Test Method
    participant Ext as Database/External
    participant SUT as System Under Test
    participant Kafka as Kafka
    participant JS as MessageHookStep

    Test->>JS: Create MessageHook (ExpectedMessageKey = "Fixture_123")
    Test->>Ext: Trigger Action
    Ext->>SUT: Event
    SUT->>Kafka: Produce Event (Key = "Fixture_123")
    Kafka->>JS: Consume
    Note over JS: Filter by ExpectedMessageKey
    Test->>JS: ExecuteAsync() (Wait)
    JS->>Test: Return Matched Message
```

## 6. Configuration & Infrastructure

The framework integrates natively with .NET Core `IConfiguration` and `IServiceCollection`.

*   **Dependency Injection**: All components are registered via `services.AddKafkaMessageHook()`.
*   **Configuration Sources**: Supports `appsettings.json`, Environment Variables, and Placeholder resolution (Steeltoe) for secret management.
*   **Consumer Groups**: Tests dynamically create and (crucially) **must clean up** consumer groups to ensure test isolation and repeatability.

## 7. Technology Stack

*   **Runtime**: .NET 6/8
*   **Messaging**: `Confluent.Kafka`, `KafkaFlow`
*   **Configuration**: `Microsoft.Extensions.Configuration`, `Steeltoe.Extensions.Configuration.Placeholder`
*   **Logging**: `Serilog`, `Microsoft.Extensions.Logging`


