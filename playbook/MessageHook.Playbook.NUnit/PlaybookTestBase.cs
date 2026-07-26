using MessageHook.Playbook.Execution;
using MessageHook.Playbook.Results;
using NUnit.Framework;

namespace MessageHook.Playbook.NUnit;

/// <summary>
/// Base fixture that runs a playbook file and asserts on its <see cref="PlaybookResult"/>. Derive from it and
/// expose a <c>[TestCaseSource]</c> that yields playbook paths via <see cref="PlaybookTestCases.Discover"/>:
/// <code>
/// public class MyPlaybooks : PlaybookTestBase
/// {
///     private static IEnumerable&lt;TestCaseData&gt; Cases() =>
///         PlaybookTestCases.Discover(TestContext.CurrentContext.TestDirectory);
///
///     [TestCaseSource(nameof(Cases))]
///     public Task Run(string path) => RunPlaybookAsync(path);
/// }
/// </code>
/// </summary>
public abstract class PlaybookTestBase
{
    protected virtual PlaybookRunner CreateRunner() => new();

    protected virtual PlaybookRunOptions CreateOptions() => new();

    protected async Task RunPlaybookAsync(string playbookPath)
    {
        var runner = CreateRunner();
        PlaybookResult result = await runner.RunAsync(playbookPath, CreateOptions());

        TestContext.WriteLine(result.Summary());
        Assert.That(result.Passed, Is.True, result.Summary());
    }
}
