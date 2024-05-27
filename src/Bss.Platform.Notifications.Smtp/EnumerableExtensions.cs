namespace Bss.Platform.Notifications.Smtp;

internal static class EnumerableExtensions
{
    public static void AddRange<T>(this ICollection<T> source, IEnumerable<T> args)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(args);

        foreach (var item in args)
        {
            source.Add(item);
        }
    }

    public static string Join<T>(this IEnumerable<T> source, string separator)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(separator);

        return string.Join(separator, source);
    }
}
