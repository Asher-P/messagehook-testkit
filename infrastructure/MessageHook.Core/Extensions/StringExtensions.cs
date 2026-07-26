namespace MessageHook.Core.Extensions;

public static class StringExtensions
{
    public static string AddPrefix(this string source, string prefix)
    {
        return source.Insert(0, prefix);
    }
}