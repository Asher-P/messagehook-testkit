using NUnit.Framework;

namespace MessageHook.Playbook.NUnit;

/// <summary>
/// Discovers <c>*.playbook.json</c> files and turns each into one <see cref="TestCaseData"/>, so every scenario
/// shows up as its own NUnit test. Wire it into a fixture with <c>[TestCaseSource]</c>.
/// </summary>
public static class PlaybookTestCases
{
    public const string DefaultPattern = "*.playbook.json";

    public static IEnumerable<TestCaseData> Discover(string directory, string pattern = DefaultPattern)
    {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var path in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories).OrderBy(p => p))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            yield return new TestCaseData(path).SetName($"Playbook_{name}");
        }
    }
}
