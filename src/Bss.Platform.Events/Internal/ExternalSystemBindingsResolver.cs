using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Models;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Events.Internal;

public class ExternalSystemBindingsResolver(
    IEventTypeProvider eventTypeProvider,
    IOptions<ExternalSystemBindingsOptions> options) : IExternalSystemBindingsResolver
{
    /// <summary>
    /// Resolves which output-event routing keys each configured queue should be bound to.<br/>
    /// <c>null</c>/empty <see cref="ExternalSystemBindingsOptions.SystemBindings"/> values fall back to the default
    /// set: all <see cref="IEventTypeProvider.OutputEvents"/> except those matching <see cref="ExternalSystemBindingsOptions.ExcludeOutputEvents"/>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveQueueBindings()
    {
        var defaultRoutingKeys = eventTypeProvider.OutputEvents.Values
            .Distinct()
            .Where(routingKey => !options.Value.ExcludeOutputEvents.Any(pattern => WildcardMatcher.IsMatch(routingKey, pattern)))
            .ToArray();

        return options.Value.SystemBindings.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<string>)(x.Value is { Length: > 0 } explicitRoutingKeys ? explicitRoutingKeys : defaultRoutingKeys));
    }

    public IReadOnlyDictionary<string, Type> ResolveOutputEventsForExport()
    {
        var allUniqOutputRoutingKeys = this.ResolveQueueBindings().Values.SelectMany(x => x).Distinct();
        return eventTypeProvider.OutputEvents.Where(x => allUniqOutputRoutingKeys.Contains(x.Value)).ToDictionary(x => x.Value, x => x.Key);
    }

    public IReadOnlyDictionary<string, Type> ResolveInputEventsForExport()
    {
        return eventTypeProvider.InputEvents.ToDictionary(x => x.Value, x => x.Key);
    }

/*
    public IReadOnlyDictionary<string, Type> ResolveOutputEventsForExport(ExternalSystemBindingsOptions options) =>
        eventTypeProvider.OutputEvents
            .Where(x => !this.IsExcluded(x.Value, options.ExcludeOutputEvents))
            .GroupBy(x => x.Value, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().Key, StringComparer.Ordinal);
*/
/*
    private string[] ResolveDefaultRoutingKeys(ExternalSystemBindingsOptions options) =>
        eventTypeProvider.OutputEvents.Values
            .Distinct()
            .Where(routingKey => !this.IsExcluded(routingKey, options.ExcludeOutputEvents))
            .ToArray();

    private bool IsExcluded(string routingKey, string[] excludeOutputEvents) =>
        excludeOutputEvents.Any(pattern => WildcardMatcher.IsMatch(routingKey, pattern));
        */
}
