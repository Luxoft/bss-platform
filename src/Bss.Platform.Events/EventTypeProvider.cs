using System.Reflection;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;

namespace Bss.Platform.Events;

internal class EventTypeProvider<T> : IEventTypeProvider, IIntegrationEventSetup<T>
{
    public IReadOnlyDictionary<Type, string> InternalEvents => this.internalTypes;
    public IReadOnlyDictionary<Type, string> ExternalEvents => this.externalTypes;

    private readonly Dictionary<Type, string> internalTypes = [];
    private readonly Dictionary<Type, string> externalTypes = [];

    public IIntegrationEventSetup<T> AddInternalEvents<TEvent>(string prefix = "", params Assembly[] assemblies)
        where TEvent : T
    {
        var newTypes = GetOrDefaultAssembly<TEvent>(assemblies)
            .SelectMany(x => x.DefinedTypes)
            .Where(IsAssignableAndSatisfyCondition<TEvent>)
            .Except(this.externalTypes.Keys);

        foreach (var newType in newTypes)
        {
            this.internalTypes[newType] = $"{prefix}{newType.Name}";
        }

        return this;
    }

    public IIntegrationEventSetup<T> AddInternalEvent<TEvent>(string routingKey)
        where TEvent : class, T
    {
        var type = typeof(TEvent);
        this.internalTypes[type] = routingKey;
        return this;
    }

    public IIntegrationEventSetup<T> AddExternalEvents<TEvent>(string prefix = "", params Assembly[] assemblies)
        where TEvent : T
    {
        var newTypes = GetOrDefaultAssembly<TEvent>(assemblies)
            .SelectMany(x => x.DefinedTypes)
            .Where(IsAssignableAndSatisfyCondition<TEvent>)
            .Except(this.externalTypes.Keys);

        foreach (var newType in newTypes)
        {
            this.externalTypes[newType] = $"{prefix}{newType.Name}";
        }

        return this;
    }

    public IIntegrationEventSetup<T> AddExternalEvent<TEvent>(string routingKey)
        where TEvent : class, T
    {
        var type = typeof(TEvent);
        this.externalTypes[type] = routingKey;
        return this;
    }

    private static Assembly[] GetOrDefaultAssembly<TEvent>(Assembly[] assemblies) where TEvent : T
    {
        if (assemblies.Length == 0)
        {
            assemblies = [typeof(TEvent).Assembly];
        }

        return assemblies;
    }

    private static bool IsAssignableAndSatisfyCondition<TAssignableTo>(TypeInfo typeInfo) =>
        typeInfo is { IsInterface: false, IsAbstract: false, IsNested: false }
        && typeof(TAssignableTo).IsAssignableFrom(typeInfo)
        && !typeInfo.Name.Contains('<');
}
