using System.Text.RegularExpressions;

using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Options;

namespace Bss.Platform.RabbitMq.Services;

public class OutputBindingsResolver(IOutputEventCatalog catalog, IOptions<RabbitMqProducerSettings> options) : IOutputBindingsResolver
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveQueueBindings()
    {
        var settings = options.Value;
        var defaults = catalog.Events.Keys.Where(route => !settings.DefaultExcludeOutputBindings.Any(pattern => Matches(route, pattern))).ToArray();
        return (settings.SystemBindings ?? []).ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<string>)(x.Value is { Length: > 0 } routes ? routes : defaults).Distinct(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, Type> ResolveSchemaEvents()
    {
        if (options.Value.SystemBindings is null) return catalog.Events;
        var selected = this.ResolveQueueBindings().Values.SelectMany(x => x).ToHashSet(StringComparer.Ordinal);
        return catalog.Events.Where(x => selected.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }

    private static bool Matches(string route, string pattern) => Regex.IsMatch(
        route, $"\\A{Regex.Escape(pattern).Replace("\\*", ".*")}\\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
}
