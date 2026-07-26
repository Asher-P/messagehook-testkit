using System.Text.Json.Nodes;
using MessageHook.Orchestration.Entities.Enums;
using MessageHook.Playbook;
using MessageHook.Playbook.Execution;
using MessageHook.Playbook.Loading;
using MessageHook.Playbook.Models;
using MessageHook.Playbook.Validation;

namespace MessageHook.NUnit;

/// <summary>
/// Broker-free unit tests for the playbook pipeline: parsing/round-trip, validation, payload resolution,
/// placeholders, and path navigation. None of these connect to Kafka — they prove a UI/service can load,
/// validate, and resolve payloads (including entirely in memory) before any broker is involved.
/// </summary>
public class PlaybookUnitTests
{
    private readonly PlaybookLoader _loader = new();

    private const string EchoJson = """
    {
      "Name": "unit",
      "KafkaConfiguration": { "BootstrapServers": [ "localhost:9092" ], "ConsumerGroup": "g" },
      "ConsumeTopics": [ "B" ],
      "ProduceTopics": [ { "topic": "A", "serializer": "Json" } ],
      "Test": { "Steps": [ {
        "Name": "s1", "ProduceTo": "A", "ConsumeFrom": [ "B" ],
        "Send": { "id": 1, "name": "seed" },
        "Override": { "name": "Buddy" },
        "Validations": [ { "path": "name", "type": "Equals", "expected": "Buddy" } ]
      } ] }
    }
    """;

    // --- parsing / round-trip -------------------------------------------------------------------------

    [Test]
    public void Loads_topic_and_send_shorthands()
    {
        var def = _loader.Load(EchoJson);

        Assert.That(def.ConsumeTopics[0].Topic, Is.EqualTo("B"));
        Assert.That(def.ConsumeTopics[0].Serializer, Is.EqualTo("Json"));   // string shorthand → default serializer
        Assert.That(def.ProduceTopics[0].Topic, Is.EqualTo("A"));
        Assert.That(def.Test.Steps[0].Send!.Inline, Is.Not.Null);           // inline object payload
        Assert.That(def.Test.Steps[0].Match?.Mode ?? MatchMode.CorrelationId, Is.EqualTo(MatchMode.CorrelationId));
    }

    [Test]
    public void Round_trips_losslessly()
    {
        var def = _loader.Load(EchoJson);
        var reloaded = _loader.Load(_loader.Serialize(def));

        Assert.That(reloaded.ConsumeTopics[0].Topic, Is.EqualTo("B"));
        Assert.That(reloaded.ProduceTopics[0].Topic, Is.EqualTo("A"));
        Assert.That(reloaded.Test.Steps[0].Send!.Inline!["name"]!.GetValue<string>(), Is.EqualTo("seed"));
        Assert.That(reloaded.Test.Steps[0].Validations![0].Type, Is.EqualTo("Equals"));
    }

    // --- validation (broker-free) ---------------------------------------------------------------------

    [Test]
    public void Validate_passes_for_in_memory_inline_definition()
    {
        var runner = new PlaybookRunner();
        var provider = new InMemoryPayloadProvider(new Dictionary<string, string>());
        Assert.DoesNotThrow(() => runner.Validate(_loader.Load(EchoJson), provider));
    }

    [Test]
    public void Validate_rejects_undeclared_consume_topic()
    {
        var json = EchoJson.Replace("\"ConsumeFrom\": [ \"B\" ]", "\"ConsumeFrom\": [ \"Z\" ]");
        var ex = Assert.Throws<PlaybookException>(() => new PlaybookRunner().Validate(_loader.Load(json)));
        Assert.That(ex!.Message, Does.Contain("not declared"));
    }

    [Test]
    public void Validate_rejects_consume_only_with_correlation_id()
    {
        var json = """
        {
          "KafkaConfiguration": { "BootstrapServers": [ "localhost:9092" ] },
          "ConsumeTopics": [ "B" ], "ProduceTopics": [],
          "Test": { "Steps": [ { "Name": "c", "ConsumeFrom": [ "B" ], "Match": { "mode": "CorrelationId" } } ] }
        }
        """;
        var ex = Assert.Throws<PlaybookException>(() => new PlaybookRunner().Validate(_loader.Load(json)));
        Assert.That(ex!.Message, Does.Contain("CorrelationId matching requires a ProduceTo"));
    }

    private const string ProduceOnlyJson = """
    {
      "KafkaConfiguration": { "BootstrapServers": [ "localhost:9092" ] },
      "ConsumeTopics": [ "B" ], "ProduceTopics": [ { "topic": "A" } ],
      "Test": { "Steps": [ {
        "Name": "seed", "ProduceTo": "A", "Key": "{{guid}}",
        "Send": { "id": 1, "name": "cat" }
      } ] }
    }
    """;

    /// <summary>
    /// Every shipped scenario passes broker-free validation, so a malformed playbook is caught here rather than
    /// as a confusing failure in <see cref="PlaybookScenarioTests"/>, which needs a live Kafka.
    /// </summary>
    [Test]
    public void Validate_passes_for_every_shipped_playbook()
    {
        var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Playbooks");
        var files = Directory.GetFiles(directory, "*.playbook.json");
        Assert.That(files, Is.Not.Empty, "no playbooks were copied to the test output");

        var runner = new PlaybookRunner();
        var provider = new FileSystemPayloadProvider(directory);

        Assert.Multiple(() =>
        {
            foreach (var file in files)
                Assert.DoesNotThrow(() => runner.Validate(_loader.LoadFromFile(file), provider), Path.GetFileName(file));
        });
    }

    [Test]
    public void Validate_accepts_produce_only_step()
    {
        Assert.DoesNotThrow(() => new PlaybookRunner().Validate(_loader.Load(ProduceOnlyJson)));
    }

    /// <summary>
    /// The topics decide the shape, not the count — a step with no ConsumeFrom never waits, so any
    /// ExpectedMessageCount it carries (the old produce-only marker 0, or anything else) is dropped on load
    /// rather than rejected, and never comes back out when the definition is re-emitted.
    /// </summary>
    [Test]
    public void Drops_expected_count_from_a_produce_only_step()
    {
        foreach (var count in new[] { 0, 5 })
        {
            var json = ProduceOnlyJson.Replace("\"Key\":", $"\"ExpectedMessageCount\": {count}, \"Key\":");
            var def = _loader.Load(json);
            var step = def.Test.Steps[0];

            Assert.DoesNotThrow(() => new PlaybookRunner().Validate(def), $"count {count}");
            Assert.That(step.HookType, Is.EqualTo(MessageHookType.ProduceAndForget));
            Assert.That(step.ExpectedMessageCount, Is.Null);
            Assert.That(step.EffectiveMessageCount, Is.Zero);
            Assert.That(_loader.Serialize(def), Does.Not.Contain("ExpectedMessageCount"));
        }
    }

    [Test]
    public void Hook_type_follows_the_topics()
    {
        var produceAndWait = _loader.Load(EchoJson).Test.Steps[0];
        var produceOnly = _loader.Load(ProduceOnlyJson).Test.Steps[0];
        var consumeOnly = new PlaybookStep { ConsumeFrom = new List<string> { "B" } };

        Assert.Multiple(() =>
        {
            Assert.That(produceAndWait.HookType, Is.EqualTo(MessageHookType.ProduceAndWait));
            Assert.That(produceOnly.HookType, Is.EqualTo(MessageHookType.ProduceAndForget));
            Assert.That(consumeOnly.HookType, Is.EqualTo(MessageHookType.ConsumeOnly));
        });
    }

    [Test]
    public void Validate_rejects_produce_only_step_with_validations()
    {
        var json = ProduceOnlyJson.Replace(
            "\"Send\":",
            "\"Validations\": [ { \"path\": \"name\", \"type\": \"Exists\" } ], \"Send\":");
        var ex = Assert.Throws<PlaybookException>(() => new PlaybookRunner().Validate(_loader.Load(json)));
        Assert.That(ex!.Message, Does.Contain("cannot have Validations"));
    }

    [Test]
    public void Validate_rejects_waiting_step_that_expects_zero_messages()
    {
        var json = EchoJson.Replace("\"Name\": \"s1\"", "\"ExpectedMessageCount\": 0, \"Name\": \"s1\"");
        var ex = Assert.Throws<PlaybookException>(() => new PlaybookRunner().Validate(_loader.Load(json)));
        Assert.That(ex!.Message, Does.Contain("ExpectedMessageCount must be at least 1"));
    }

    [Test]
    public void Validate_rejects_step_that_neither_produces_nor_consumes()
    {
        var json = """
        {
          "KafkaConfiguration": { "BootstrapServers": [ "localhost:9092" ] },
          "ConsumeTopics": [ "B" ], "ProduceTopics": [ { "topic": "A" } ],
          "Test": { "Steps": [ { "Name": "nothing" } ] }
        }
        """;
        var ex = Assert.Throws<PlaybookException>(() => new PlaybookRunner().Validate(_loader.Load(json)));
        Assert.That(ex!.Message, Does.Contain("must produce (ProduceTo) or consume (ConsumeFrom)"));
    }

    [Test]
    public void Validate_rejects_unknown_validation_type()
    {
        var json = EchoJson.Replace("\"type\": \"Equals\"", "\"type\": \"Frobnicate\"");
        var ex = Assert.Throws<PlaybookException>(() => new PlaybookRunner().Validate(_loader.Load(json)));
        Assert.That(ex!.Message, Does.Contain("unknown validation type"));
    }

    [Test]
    public void Validate_rejects_strict_override_typo()
    {
        var json = """
        {
          "KafkaConfiguration": { "BootstrapServers": [ "localhost:9092" ] },
          "ConsumeTopics": [ "B" ], "ProduceTopics": [ { "topic": "A" } ],
          "StrictOverride": true,
          "Test": { "Steps": [ {
            "ProduceTo": "A", "ConsumeFrom": [ "B" ],
            "Send": { "name": "seed" },
            "Override": { "typoField": "x" }
          } ] }
        }
        """;
        var ex = Assert.Throws<PlaybookException>(() => new PlaybookRunner().Validate(_loader.Load(json)));
        Assert.That(ex!.Message, Does.Contain("StrictOverride"));
    }

    // --- payload resolution (no filesystem) -----------------------------------------------------------

    [Test]
    public void Override_patches_matching_paths_and_registers_the_rest_as_variables()
    {
        var provider = new InMemoryPayloadProvider(new Dictionary<string, string>
        {
            ["p.json"] = """{ "id": "seed", "name": "seed", "owner": { "city": "x" } }"""
        });
        var resolver = new PayloadResolver(new PlaceholderExpander());
        var context = new PlaybookContext();

        var overrides = new Dictionary<string, JsonNode?>
        {
            ["name"] = JsonValue.Create("Buddy"),
            ["owner.city"] = JsonValue.Create("Tel Aviv"),
            ["greeting"] = JsonValue.Create("hello")     // matches no path → becomes a variable
        };

        var node = resolver.Resolve(new SendDefinition { File = "p.json" }, overrides, context, provider)!;

        Assert.That(node["name"]!.GetValue<string>(), Is.EqualTo("Buddy"));
        Assert.That(node["owner"]!["city"]!.GetValue<string>(), Is.EqualTo("Tel Aviv"));
        Assert.That(context.Variables["greeting"], Is.EqualTo("hello"));
        Assert.That(context.Variables.ContainsKey("name"), Is.False);
    }

    [Test]
    public void Override_variable_is_expanded_inside_the_payload()
    {
        var provider = new InMemoryPayloadProvider(new Dictionary<string, string>
        {
            ["p.json"] = """{ "name": "{{who}}" }"""
        });
        var resolver = new PayloadResolver(new PlaceholderExpander());
        var context = new PlaybookContext();
        var overrides = new Dictionary<string, JsonNode?> { ["who"] = JsonValue.Create("Zed") };

        var node = resolver.Resolve(new SendDefinition { File = "p.json" }, overrides, context, provider)!;

        Assert.That(node["name"]!.GetValue<string>(), Is.EqualTo("Zed"));
    }

    [Test]
    public void Missing_payload_file_fails_at_load()
    {
        var resolver = new PayloadResolver(new PlaceholderExpander());
        Assert.Throws<PlaybookException>(() => resolver.Resolve(
            new SendDefinition { File = "nope.json" },
            new Dictionary<string, JsonNode?>(),
            new PlaybookContext(),
            new InMemoryPayloadProvider(new Dictionary<string, string>())));
    }

    // --- placeholders & path navigation ---------------------------------------------------------------

    [Test]
    public void Placeholders_expand_env_default_guid_and_variables()
    {
        var expander = new PlaceholderExpander();
        var vars = new Dictionary<string, string> { ["x"] = "y" };

        Assert.That(expander.ExpandString("${NO_SUCH_ENV_VAR_12345:def}", vars), Is.EqualTo("def"));
        Assert.That(Guid.TryParse(expander.ExpandString("{{guid}}", vars), out _), Is.True);
        Assert.That(expander.ExpandString("v={{x}}", vars), Is.EqualTo("v=y"));
        Assert.Throws<PlaybookException>(() => expander.ExpandString("{{missing}}", vars));
    }

    [Test]
    public void Path_resolver_navigates_nested_arrays_and_reports_misses()
    {
        object root = new Dictionary<string, object>
        {
            ["a"] = new Dictionary<string, object>
            {
                ["b"] = new object[] { new Dictionary<string, object> { ["c"] = 5.0 } }
            }
        };

        Assert.That(MessagePathResolver.TryResolve(root, "a.b[0].c", out var value), Is.True);
        Assert.That(value, Is.EqualTo(5.0));
        Assert.That(MessagePathResolver.TryResolve(root, "a.x", out _), Is.False);
    }
}
