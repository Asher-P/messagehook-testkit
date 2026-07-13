namespace MessageHook.Core.Extensions;

public static class CollectionExtensions
{
    public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection)
        => collection == null || !collection.Any();

    public static int GetListHash<T>(this IEnumerable<T> list, IEqualityComparer<T> comparer = null)
    {
        var hashCode = new HashCode();
        foreach (var item in list ?? Enumerable.Empty<T>())
            hashCode.Add(item, comparer);
        return hashCode.ToHashCode();
    }
}
