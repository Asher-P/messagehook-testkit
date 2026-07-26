using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MessageHook.EchoService.Tracking;

/// <summary>
/// Stamps an <c>IsChanged</c> flag onto an echoed payload: true when this id's name differs from the name last
/// echoed for the same id, false the first time an id is seen. Kept apart from the Kafka middleware so the rule
/// can be exercised without a broker.
/// </summary>
public sealed class PayloadChangeStamper
{
    /// <summary>Flag added to the payload. Exact casing matters — playbook payload paths are case-sensitive.</summary>
    public const string IsChangedField = "IsChanged";

    private const string IdField = "id";
    private const string NameField = "name";

    private readonly MessageChangeTracker _tracker;
    private readonly ILogger<PayloadChangeStamper> _logger;

    public PayloadChangeStamper(MessageChangeTracker tracker, ILogger<PayloadChangeStamper> logger)
    {
        _tracker = tracker;
        _logger = logger;
    }

    /// <summary>
    /// Returns the payload with <see cref="IsChangedField"/> set. Anything that isn't a JSON object is returned
    /// byte-for-byte, so the echo stays a pass-through for payloads it doesn't understand.
    /// </summary>
    public byte[] Stamp(byte[] payload, string? messageKey)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (JsonException e)
        {
            _logger.LogWarning("Payload is not JSON, echoing it unchanged: {Reason}", e.Message);
            return payload;
        }

        if (node is not JsonObject obj)
        {
            _logger.LogWarning("Payload is not a JSON object, echoing it unchanged.");
            return payload;
        }

        var name = FindValue(obj, NameField)?.ToString();

        // The id names the thing being tracked; fall back to the Kafka key when the payload carries no id.
        var id = FindValue(obj, IdField)?.ToString() ?? messageKey;

        var changed = false;
        if (string.IsNullOrEmpty(id))
            _logger.LogWarning("Message has neither an '{IdField}' nor a key, so no change can be detected; " +
                               "stamping {Field}=false.", IdField, IsChangedField);
        else
            changed = _tracker.RecordAndDetectChange(id, name);

        // Never trust an inbound flag — drop any casing of it, then stamp the value we computed.
        foreach (var stale in obj.Select(p => p.Key)
                     .Where(k => k.Equals(IsChangedField, StringComparison.OrdinalIgnoreCase))
                     .ToList())
            obj.Remove(stale);

        obj[IsChangedField] = changed;

        _logger.LogInformation("Change check: id={Id} name={Name} {Field}={Changed}", id, name, IsChangedField, changed);

        return Encoding.UTF8.GetBytes(obj.ToJsonString());
    }

    /// <summary>Looks a field up case-insensitively, so 'id'/'Id' and 'name'/'Name' payloads both work.</summary>
    private static JsonNode? FindValue(JsonObject obj, string field)
    {
        foreach (var property in obj)
            if (property.Key.Equals(field, StringComparison.OrdinalIgnoreCase))
                return property.Value;

        return null;
    }
}
