using MessageHook.Playbook.NUnit;

namespace MessageHook.NUnit;

/// <summary>
/// Integration fixture: runs every <c>*.playbook.json</c> under <c>Playbooks/</c> (copied to the test output)
/// as its own NUnit test. Requires a reachable Kafka and the EchoService (A→B). Mirrors the hand-written
/// <see cref="GeneralTests"/> in its broker requirement.
/// </summary>
public class PlaybookScenarioTests : PlaybookTestBase
{
    private static IEnumerable<TestCaseData> Scenarios() =>
        PlaybookTestCases.Discover(Path.Combine(TestContext.CurrentContext.TestDirectory, "Playbooks"));

    [TestCaseSource(nameof(Scenarios))]
    public Task Run(string playbookPath) => RunPlaybookAsync(playbookPath);
}
