using System.Collections.Concurrent;
using System.Text.Json;
using MessageHook.Playbook.Loading;

namespace MessageHook.Playbook.Service.Storage;

/// <summary>
/// File-backed suite repository on the /data volume. Each suite is a folder:
/// <c>suites/&lt;id&gt;/suite.json</c> plus <c>suites/&lt;id&gt;/payloads/*.json</c>. Human-inspectable and
/// hand-editable. Per-suite locking keeps concurrent edits single-writer (last write wins).
/// </summary>
public sealed class SuiteStore
{
    private readonly string _root;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private static readonly JsonSerializerOptions Json = PlaybookLoader.Options;

    public SuiteStore(string dataDirectory)
    {
        _root = Path.Combine(dataDirectory, "suites");
        Directory.CreateDirectory(_root);
    }

    public string SuiteDirectory(string id) => Path.Combine(_root, Sanitize(id));
    public string PayloadsDirectory(string id) => Path.Combine(SuiteDirectory(id), "payloads");
    private string SuiteFile(string id) => Path.Combine(SuiteDirectory(id), "suite.json");

    // --- suites ---------------------------------------------------------------------------------------

    public IEnumerable<SuiteSummary> List()
    {
        if (!Directory.Exists(_root)) yield break;
        foreach (var dir in Directory.EnumerateDirectories(_root))
        {
            var id = Path.GetFileName(dir);
            var suite = TryLoad(id);
            if (suite is not null)
                yield return new SuiteSummary(suite.Id, suite.Name, suite.TestCases.Count, suite.Payloads.Count,
                    string.Join(",", suite.Kafka.BootstrapServers));
        }
    }

    public Suite? Get(string id) => TryLoad(id);

    public async Task<Suite> SaveAsync(Suite suite)
    {
        if (string.IsNullOrWhiteSpace(suite.Id))
            suite.Id = Guid.NewGuid().ToString("N");

        var gate = _locks.GetOrAdd(suite.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(SuiteDirectory(suite.Id));
            Directory.CreateDirectory(PayloadsDirectory(suite.Id));
            suite.Payloads = ListPayloads(suite.Id).ToList();   // never trust the client's payload list
            Normalize(suite);
            await File.WriteAllTextAsync(SuiteFile(suite.Id), JsonSerializer.Serialize(suite, Json));
            return suite;
        }
        finally
        {
            gate.Release();
        }
    }

    public bool Delete(string id)
    {
        var dir = SuiteDirectory(id);
        if (!Directory.Exists(dir)) return false;
        Directory.Delete(dir, recursive: true);
        return true;
    }

    private Suite? TryLoad(string id)
    {
        var file = SuiteFile(id);
        if (!File.Exists(file)) return null;
        var suite = JsonSerializer.Deserialize<Suite>(File.ReadAllText(file), Json);
        if (suite is null) return null;
        suite.Payloads = ListPayloads(id).ToList();
        Normalize(suite);
        return suite;
    }

    /// <summary>
    /// Drops per-step fields the step's shape cannot use — chiefly the <c>ExpectedMessageCount: 0</c> that used
    /// to mark a produce-only step. Run on load as well as save so a suite written before the topics decided the
    /// shape stops showing it in the editor, without waiting for the next save.
    /// </summary>
    private static void Normalize(Suite suite)
    {
        foreach (var step in suite.TestCases.SelectMany(c => c.Steps))
            step.Normalize();
    }

    // --- payload stack --------------------------------------------------------------------------------

    public IEnumerable<string> ListPayloads(string id)
    {
        var dir = PayloadsDirectory(id);
        if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
        return Directory.EnumerateFiles(dir, "*.json").Select(Path.GetFileName).Where(n => n is not null)!.OrderBy(n => n)!;
    }

    public async Task SavePayloadAsync(string id, string fileName, Stream content)
    {
        var safe = SanitizePayloadName(fileName);
        Directory.CreateDirectory(PayloadsDirectory(id));
        var path = Path.Combine(PayloadsDirectory(id), safe);

        // Validate it's actually JSON before storing, so a run never fails on a corrupt payload later.
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync();
        try { using var _ = JsonDocument.Parse(text); }
        catch (JsonException e) { throw new InvalidDataException($"'{fileName}' is not valid JSON: {e.Message}"); }

        await File.WriteAllTextAsync(path, text);
    }

    public string? ReadPayload(string id, string name)
    {
        var path = Path.Combine(PayloadsDirectory(id), SanitizePayloadName(name));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public bool DeletePayload(string id, string name)
    {
        var path = Path.Combine(PayloadsDirectory(id), SanitizePayloadName(name));
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    // --- path safety ----------------------------------------------------------------------------------

    private static string Sanitize(string id) => Path.GetFileName(id); // strips any directory components

    private static string SanitizePayloadName(string name)
    {
        var justName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(justName))
            throw new InvalidDataException("Payload name is empty.");
        if (!justName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            justName += ".json";
        return justName;
    }
}
