using System.Text.Json.Nodes;
using MessageHook.Playbook.Models;

namespace MessageHook.Playbook.Service.Storage;

/// <summary>
/// A test suite = the environment. Holds the Kafka config and declared topics once, plus the uploaded payload
/// stack and the suite's test cases. Reuses the library model types (<see cref="KafkaConfigDefinition"/>,
/// <see cref="TopicDeclaration"/>) so it serializes with the same shape as a playbook file.
/// </summary>
public sealed class Suite
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled suite";

    public KafkaConfigDefinition Kafka { get; set; } = new();
    public List<TopicDeclaration> ConsumeTopics { get; set; } = new();
    public List<TopicDeclaration> ProduceTopics { get; set; } = new();

    /// <summary>Uploaded payload file names (the "payload stack"). Recomputed from disk by the store, not trusted from input.</summary>
    public List<string> Payloads { get; set; } = new();

    public List<TestCase> TestCases { get; set; } = new();
}

/// <summary>A test case = a runnable playbook: ordered steps + optional suite-wide override/strict flags.</summary>
public sealed class TestCase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled case";

    /// <summary>Free-text description shown in the test-case table. Metadata only — the runner ignores it.</summary>
    public string? Description { get; set; }

    public List<PlaybookStep> Steps { get; set; } = new();

    public Dictionary<string, JsonNode?>? Override { get; set; }
    public bool StrictOverride { get; set; }
}

/// <summary>Lightweight row for the board listing.</summary>
public sealed record SuiteSummary(string Id, string Name, int TestCaseCount, int PayloadCount, string Bootstrap);
