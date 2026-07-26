using System.Text.Json;
using System.Text.Json.Serialization;
using MessageHook.Playbook.Models;

namespace MessageHook.Playbook.Loading;

/// <summary>
/// Parses playbook JSON into <see cref="PlaybookDefinition"/> and back. Accepts a file path, a raw string, or a
/// stream, so a UI/service can drive it without ever writing a temp file. Serialization is symmetric — a loaded
/// definition re-serializes losslessly (topic and Send shorthands survive) for load→edit→re-emit round-trips.
/// </summary>
public sealed class PlaybookLoader
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        return options;
    }

    public PlaybookDefinition Load(string json)
    {
        try
        {
            return Deserialize(json);
        }
        catch (JsonException e)
        {
            throw new PlaybookException($"Playbook JSON is malformed: {e.Message}");
        }
    }

    public PlaybookDefinition LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new PlaybookException($"Playbook file not found: '{path}'.");
        return Load(File.ReadAllText(path));
    }

    public PlaybookDefinition LoadFromStream(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Load(reader.ReadToEnd());
    }

    public string Serialize(PlaybookDefinition definition) => JsonSerializer.Serialize(definition, Options);

    private static PlaybookDefinition Deserialize(string json)
    {
        var definition = JsonSerializer.Deserialize<PlaybookDefinition>(json, Options)
                         ?? throw new PlaybookException("Playbook JSON deserialized to null.");

        // A hand-written file may still carry the old produce-only marker (ExpectedMessageCount 0). The topics
        // decide the shape now, so drop it here — the definition then re-serializes the way it actually runs.
        foreach (var step in definition.Test.Steps)
            step.Normalize();

        return definition;
    }
}
