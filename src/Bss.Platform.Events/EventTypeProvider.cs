using System.Reflection;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;

namespace Bss.Platform.Events;

internal class EventTypeProvider<TIn, TOut> : IEventTypeProvider, IIntegrationEventSetup<TIn, TOut>
{
    public IReadOnlyDictionary<Type, string> InputEvents => this.inputTypes;
    public IReadOnlyDictionary<Type, string> OutputEvents => this.outputTypes;

    private readonly Dictionary<Type, string> inputTypes = [];
    private readonly Dictionary<Type, string> outputTypes = [];

    public IIntegrationEventSetup<TIn, TOut> AddInputEvents<TEvent>(string prefix = "", params Assembly[] assemblies)
        where TEvent : TIn
    {
        var newTypes = GetOrDefaultAssembly<TEvent>(assemblies)
            .SelectMany(x => x.DefinedTypes)
            .Where(IsAssignableAndSatisfyCondition<TEvent>)
            .Except(this.outputTypes.Keys);

        foreach (var newType in newTypes)
        {
            this.inputTypes[newType] = $"{prefix}{newType.Name}";
        }

        return this;
    }

    public IIntegrationEventSetup<TIn, TOut> AddInputEvent<TEvent>(string routingKey)
        where TEvent : class, TIn
    {
        var type = typeof(TEvent);
        this.inputTypes[type] = routingKey;
        return this;
    }

    public IIntegrationEventSetup<TIn, TOut> AddOutputEvents<TEvent>(string prefix = "", params Assembly[] assemblies)
        where TEvent : TOut
    {
        var newTypes = GetOrDefaultAssembly<TEvent>(assemblies)
            .SelectMany(x => x.DefinedTypes)
            .Where(IsAssignableAndSatisfyCondition<TEvent>)
            .Except(this.outputTypes.Keys);

        foreach (var newType in newTypes)
        {
            this.outputTypes[newType] = $"{prefix}{newType.Name}";
        }

        return this;
    }

    public IIntegrationEventSetup<TIn, TOut> AddOutputEvent<TEvent>(string routingKey)
        where TEvent : class, TOut
    {
        var type = typeof(TEvent);
        this.outputTypes[type] = routingKey;
        return this;
    }

    private static Assembly[] GetOrDefaultAssembly<TEvent>(Assembly[] assemblies)
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
