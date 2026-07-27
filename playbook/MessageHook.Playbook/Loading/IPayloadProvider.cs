namespace MessageHook.Playbook.Loading;

/// <summary>
/// Resolves a payload file reference (the <c>Send.file</c> value) to its raw JSON text. Abstracted so the core
/// runner never touches the filesystem directly: the default reads from disk (CLI/NUnit), while a hosted
/// service can back it with an uploaded-blob store or an in-request inline map.
/// </summary>
public interface IPayloadProvider
{
    /// <summary>Returns the JSON text for <paramref name="reference"/>, or throws if it cannot be resolved.</summary>
    string ReadPayload(string reference);

    bool Exists(string reference);
}

/// <summary>Default provider: reads payload files from disk relative to a base directory (the playbook's own dir).</summary>
public sealed class FileSystemPayloadProvider : IPayloadProvider
{
    private readonly string _baseDirectory;

    public FileSystemPayloadProvider(string baseDirectory) => _baseDirectory = baseDirectory;

    public string ReadPayload(string reference)
    {
        var path = Resolve(reference);
        if (!File.Exists(path))
            throw new PlaybookException($"Payload file not found: '{reference}' (resolved to '{path}').");
        return File.ReadAllText(path);
    }

    public bool Exists(string reference) => File.Exists(Resolve(reference));

    private string Resolve(string reference) =>
        Path.IsPathRooted(reference) ? reference : Path.GetFullPath(Path.Combine(_baseDirectory, reference));
}

/// <summary>In-memory provider: maps a reference name directly to JSON text. Useful for a UI/service and tests.</summary>
public sealed class InMemoryPayloadProvider : IPayloadProvider
{
    private readonly IReadOnlyDictionary<string, string> _payloads;

    public InMemoryPayloadProvider(IReadOnlyDictionary<string, string> payloads) => _payloads = payloads;

    public string ReadPayload(string reference) =>
        _payloads.TryGetValue(reference, out var json)
            ? json
            : throw new PlaybookException($"Payload '{reference}' was not supplied to the in-memory provider.");

    public bool Exists(string reference) => _payloads.ContainsKey(reference);
}
