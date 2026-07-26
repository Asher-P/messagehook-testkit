using MessageHook.Playbook.Models;
using MessageHook.Playbook.Service.Storage;

namespace MessageHook.Playbook.Service.Playbooks;

/// <summary>
/// Assembles a runnable <see cref="PlaybookDefinition"/> from a suite (environment) and one of its test cases.
/// Because the suite/case reuse the library model types, this is a field copy, not a translation.
/// </summary>
public static class PlaybookAssembler
{
    public static PlaybookDefinition Assemble(Suite suite, TestCase testCase) => new()
    {
        Name = $"{suite.Name} / {testCase.Name}",
        KafkaConfiguration = suite.Kafka,
        ConsumeTopics = suite.ConsumeTopics,
        ProduceTopics = suite.ProduceTopics,
        Override = testCase.Override,
        StrictOverride = testCase.StrictOverride,
        Test = new PlaybookTest { Steps = testCase.Steps }
    };
}
