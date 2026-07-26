namespace MessageHook.EchoService.Tracking;

/// <summary>
/// Remembers the last name seen for each message id, so the echo can tell whether a re-sent id carries a new
/// name. In-memory and process-lifetime only — restarting the service forgets every id, which is what a test
/// echo wants (a fresh run starts from a clean slate).
/// </summary>
public sealed class MessageChangeTracker
{
    private readonly Dictionary<string, string?> _lastNameById = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>
    /// Records <paramref name="name"/> as the current name for <paramref name="id"/> and reports whether it
    /// differs from the previous one. The first time an id is seen there is nothing to differ from, so the
    /// answer is <c>false</c>.
    /// </summary>
    public bool RecordAndDetectChange(string id, string? name)
    {
        // Consumers run with WorkersCount > 1, so read-then-write has to be one atomic step.
        lock (_gate)
        {
            var seenBefore = _lastNameById.TryGetValue(id, out var previous);
            _lastNameById[id] = name;
            return seenBefore && !string.Equals(previous, name, StringComparison.Ordinal);
        }
    }
}
