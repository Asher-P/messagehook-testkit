namespace MessageHook.Playbook.Validation;

/// <summary>One segment of a message path: either a property name or an array index.</summary>
public readonly struct PathSegment
{
    public bool IsIndex { get; }
    public string Name { get; }
    public int Index { get; }

    private PathSegment(bool isIndex, string name, int index)
    {
        IsIndex = isIndex;
        Name = name;
        Index = index;
    }

    public static PathSegment Property(string name) => new(false, name, -1);
    public static PathSegment At(int index) => new(true, string.Empty, index);

    public override string ToString() => IsIndex ? $"[{Index}]" : Name;
}

/// <summary>Parses paths like <c>a.b[0].c</c> / <c>items[2]</c> / <c>[0]</c> into ordered segments.</summary>
public static class MessagePath
{
    public static bool IsRoot(string? path) =>
        string.IsNullOrWhiteSpace(path) || path.Trim() == "$";

    public static IReadOnlyList<PathSegment> Parse(string path)
    {
        var segments = new List<PathSegment>();
        var i = 0;
        while (i < path.Length)
        {
            var c = path[i];
            if (c == '.')
            {
                i++;
                continue;
            }

            if (c == '[')
            {
                var close = path.IndexOf(']', i);
                if (close < 0)
                    throw new PlaybookException($"Malformed path '{path}': missing ']'.");
                var inner = path[(i + 1)..close].Trim();
                if (!int.TryParse(inner, out var index))
                    throw new PlaybookException($"Malformed path '{path}': '[{inner}]' is not an index.");
                segments.Add(PathSegment.At(index));
                i = close + 1;
                continue;
            }

            // property name: read until next '.' or '['
            var start = i;
            while (i < path.Length && path[i] != '.' && path[i] != '[')
                i++;
            var name = path[start..i];
            if (name.Length > 0)
                segments.Add(PathSegment.Property(name));
        }

        return segments;
    }
}
