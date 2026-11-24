using System.Reflection;

namespace Bss.Platform.RabbitMq;

/// <summary>Declared output contracts. Does not restrict business-event publication.</summary>
public class OutputEventCatalog : IOutputEventCatalog
{
    private readonly Dictionary<string, Type> events = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, Type> Events => this.events;

    public OutputEventCatalog AddOutputEvent<TEvent>(string routingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        if (!this.events.TryAdd(routingKey, typeof(TEvent)) && this.events[routingKey] != typeof(TEvent))
            throw new ArgumentException($"Output routing key '{routingKey}' is already assigned to another type.", nameof(routingKey));
        return this;
    }

    public OutputEventCatalog AddOutputEvents<TEvent>(string prefix = "", params Assembly[] assemblies)
    {
        var source = assemblies.Length == 0 ? [typeof(TEvent).Assembly] : assemblies;
        foreach (var type in source.SelectMany(x => x.DefinedTypes)
                     .Where(x => typeof(TEvent).IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface && !x.IsNested && !x.ContainsGenericParameters)
                     .OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            if (this.events.ContainsValue(type)) continue;
            var route = $"{prefix}{type.Name}";
            if (!this.events.TryAdd(route, type))
                throw new ArgumentException($"Output routing key '{route}' is already registered.");
        }

        return this;
    }
}
