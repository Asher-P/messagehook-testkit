# MessageHook.Playbook.NUnit

NUnit adapter for [MessageHook.Playbook](https://www.nuget.org/packages/MessageHook.Playbook). Discovers
`*.playbook.json` files and surfaces each as its own NUnit test.

```csharp
using MessageHook.Playbook.NUnit;
using NUnit.Framework;

public class Playbooks : PlaybookTestBase
{
    private static IEnumerable<TestCaseData> Cases() =>
        PlaybookTestCases.Discover(TestContext.CurrentContext.TestDirectory);

    [TestCaseSource(nameof(Cases))]
    public Task Run(string path) => RunPlaybookAsync(path);
}
```

Each `.playbook.json` under the test output directory becomes a separate test named `Playbook_<file>`, passing
or failing on its `PlaybookResult`. Copy your playbook and payload files to the output directory
(`CopyToOutputDirectory`) so they're discovered at test time.

Kept separate from the core library so `MessageHook.Playbook` carries no test-framework dependency.
