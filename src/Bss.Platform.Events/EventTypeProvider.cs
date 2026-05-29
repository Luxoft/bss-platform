using System.Reflection;

using Bss.Platform.Events.Interfaces;

namespace Bss.Platform.Events;

internal class EventTypeProvider<T> : IEventTypeProvider, IIntegrationEventSetup<T>
{
    public IReadOnlyDictionary<Type, string> InternalEvents => this.internalTypes;

    public IReadOnlyDictionary<Type, string> ExternalEvents => this.externalTypes;

    private readonly Dictionary<Type, string> internalTypes;
    private readonly Dictionary<Type, string> externalTypes = new();

    public EventTypeProvider(Assembly defaultAssembly)
    {
        this.internalTypes =
            defaultAssembly
                .DefinedTypes
                .Where(IsAssignableAndSatisfyCondition<T>)
                .ToDictionary(Type (x) => x, x => x.Name);
    }

    public IIntegrationEventSetup<T> AddInternalEvent<TImpl>(string routingKey) where TImpl : T
    {
        var type = typeof(TImpl);
        this.internalTypes[type] = routingKey;
        return this;
    }

    public IIntegrationEventSetup<T> AddExternalEvents<TExternalBase>(string prefix, params Assembly[] assemblies)
    {
        var newTypes = assemblies.SelectMany(x => x.DefinedTypes)
            .Where(IsAssignableAndSatisfyCondition<TExternalBase>)
            .Except(this.externalTypes.Keys);

        foreach (var newType in newTypes)
        {
            this.externalTypes[newType] = $"{prefix}{newType.Name}";
        }

        return this;
    }

    public IIntegrationEventSetup<T> AddExternalEvent<TExternalConcrete>(string routingKey)
        where TExternalConcrete : class
    {
        var type = typeof(TExternalConcrete);
        this.externalTypes[type] = routingKey;
        return this;
    }

    private static bool IsAssignableAndSatisfyCondition<TAssignableTo>(TypeInfo typeInfo) =>
        typeInfo is { IsInterface: false, IsAbstract: false, IsNested: false }
        && typeof(TAssignableTo).IsAssignableFrom(typeInfo)
        && !typeInfo.Name.Contains('<');
}
