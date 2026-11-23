using System.Text.RegularExpressions;

namespace Bss.Platform.Events.Internal;

internal static class WildcardMatcher
{
    /// <summary>
    /// Matches <paramref name="value" /> against <paramref name="pattern" />, where <c>*</c> matches any substring.<br/>
    /// Case-insensitive. A pattern without <c>*</c> requires an exact match.
    /// </summary>
    public static bool IsMatch(string value, string pattern) =>
        pattern.Contains('*')
            ? Regex.IsMatch(value, ToRegexPattern(pattern), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))
            : string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);

    private static string ToRegexPattern(string pattern) =>
        $"^{string.Join("[\\s\\S]*", pattern.Split('*').Select(Regex.Escape))}$";
}
