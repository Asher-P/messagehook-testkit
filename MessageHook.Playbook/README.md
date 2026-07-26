# MessageHook.Playbook

Run Kafka integration tests from a JSON file instead of C#. A **playbook** describes the broker, the topics in
play, and an ordered list of produce/consume steps with structured validations. The runner drives it against the
existing MessageHook.Kafka infrastructure and reports pass/fail — no test code per scenario.

The runner **connects to an existing Kafka** (cloud or self-hosted) via the playbook's `KafkaConfiguration`. It
never provisions a broker.

## Quick start

```csharp
using MessageHook.Playbook.Execution;

var runner = new PlaybookRunner();
PlaybookResult result = await runner.RunAsync("scenarios/echo.playbook.json");

if (!result.Passed)
    Console.WriteLine(result.Summary());
```

Host-agnostic — the same runner takes a file path, a `Stream`, or an in-memory `PlaybookDefinition`, so a CLI,
the NUnit adapter (`MessageHook.Playbook.NUnit`), or a web UI can all sit on top of it:

```csharp
runner.Validate(definition);                    // load-time checks, no broker connection
await runner.RunAsync(definition, new PlaybookRunOptions
{
    PayloadProvider = new InMemoryPayloadProvider(uploadedPayloads),
    Progress = new Progress<StepResult>(s => hub.Push(s)),
    CancellationToken = ct
});
```

## Playbook format

```json
{
  "Name": "animal-echo",
  "KafkaConfiguration": {
    "BootstrapServers": [ "${KAFKA_BOOTSTRAP:localhost:9092}" ],
    "Credentials": { "SecurityProtocol": "Plaintext", "TlsEnabled": false },
    "ConsumerGroup": "messagehook-playbook"
  },

  "ConsumeTopics": [ "B" ],
  "ProduceTopics": [ { "topic": "A", "serializer": "Json" } ],

  "Test": {
    "Steps": [
      {
        "Name": "echo animal",
        "ProduceTo": "A",
        "ConsumeFrom": [ "B" ],
        "Key": "{{guid}}",
        "Send": { "file": "payloads/animal.json" },
        "Override": { "id": "{{guid}}", "name": "Buddy" },
        "Match": { "mode": "CorrelationId" },
        "ExpectedMessageCount": 1,
        "TimeoutSeconds": 30,
        "Validations": [
          { "path": "name", "type": "Equals", "expected": "Buddy" },
          { "path": "id", "type": "Exists" }
        ]
      }
    ]
  }
}
```

### Topics

`ConsumeTopics` / `ProduceTopics` are declared once for the whole suite (the KafkaFlow cluster is built up
front). Each entry is a bare string or an object:

```json
{ "topic": "C", "serializer": "Json", "messageType": "My.Ns.Animal, My.Assembly", "workersCount": 55, "bufferSize": 50 }
```

- `serializer`: `Json`/`Utf8` (default) or `Protobuf`. Consuming is Json-only; Protobuf is produce-only and
  requires a `messageType`.
- `messageType`: assembly-qualified .NET type. Omit for schema-less (`Dictionary<string, object>`).

A step's `ProduceTo` / `ConsumeFrom` must reference declared topics.

### Step shapes

A step is one of three shapes, decided by which of `ProduceTo` / `ConsumeFrom` it has — the same rule the engine
applies in `BaseMessageHookStep.MessageHookType`, so a step behaves as its topics say and nothing else selects
the shape:

| Shape | `ProduceTo` | `ConsumeFrom` | `ExpectedMessageCount` | Notes |
|---|---|---|---|---|
| produce & consume (`ProduceAndWait`) | yes | yes | ≥ 1 | produce, then wait for and validate the response |
| produce only (`ProduceAndForget`) | yes | — | dropped on load (effectively 0) | fire-and-forget: produce and move on, no wait |
| consume only (`ConsumeOnly`) | — | yes | ≥ 1 | requires `Match.mode: MessageKey` |

A produce-only step returns as soon as the message is produced, so nothing comes back to assert against:
`Validations` and `Capture` are load-time errors on it. Use it to seed state, then assert in a later step.
`ExpectedMessageCount` is not what makes it produce-only — omitting `ConsumeFrom` is — so any count it carries
(including the `0` older files used as the marker) is dropped on load and never re-emitted:

```json
{ "Name": "seed cat", "ProduceTo": "A", "Key": "{{guid}}",
  "Send": { "file": "payloads/animal.json" },
  "Override": { "name": "cat" } }
```

### Matching

Only steps that consume are matched.

- `CorrelationId` (default): MessageHook injects a correlation header on produce and waits for it. Requires
  `ProduceTo`.
- `MessageKey`: waits for a message whose Kafka key equals `Match.expectedKey` (defaults to the produced key).
  Works with or without `ProduceTo` — the only mode valid for a consume-only step.

### Payloads and Override

`Send` is an inline object, a file path string, or `{ "file": "..." }`. The payload file is never mutated;
`Override` is the single mechanism that patches it and does double duty:

- an entry whose name matches a **payload path** replaces that value (`owner.city`, `tags[0]` supported);
- an entry that matches nothing is a **placeholder value**, usable as `{{name}}` anywhere.

Declarable per step, or once at file level (a step entry wins). `StrictOverride: true` makes an entry that
matches neither a payload path nor any placeholder a load-time error.

### Placeholders

Any string may contain `${ENV}` / `${ENV:default}`, `{{guid}}`, `{{now}}`, and `{{name}}` (from `Override` or a
prior step's `Capture`).

### Validations

`{ "target": "value"|"key", "path": "a.b[0].c", "type": "...", "expected": ... }`. `path` empty or `$` means the
whole payload. Types: `Equals`, `NotEquals`, `Contains`, `NotContains`, `Exists`, `NotExists`, `Matches`,
`GreaterThan`, `LessThan`, `Count`.

`Capture` pulls a value from the received message into a variable for later steps:
`"Capture": { "echoedId": "id" }`.

## Notes

- The runner caches one KafkaFlow host (bus) per Kafka config — same broker + declared topics + consumer group
  — and reuses it across runs, keyed by that config. A host is built once (its group is `{ConsumerGroup}-{suffix}`)
  and then kept running for the life of the process: it is never stopped or disposed. That is deliberate —
  stopping or disposing a KafkaFlow/`librdkafka` consumer lets its poll loop resume one last `rd_kafka_consumer_poll`
  after the native handle is gone, a use-after-free that crashes the whole process with an access violation. A
  suite's leftover consumer group is emptied when the process exits and then ages out under the broker's group
  retention.
- `Validate(...)` runs load-time checks with no broker connection — ideal for a UI validating as the user types.
