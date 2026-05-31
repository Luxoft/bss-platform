using System.Reflection;

namespace Bss.Platform.Events.Interfaces;

public interface IIntegrationEventSetup<in T>
{
    /// <summary>
    ///     Add multiple events implemented or inherited TInternalBase with the prefix
    /// </summary>
    /// <param name="prefix">
    ///     prefix to add before type name
    ///     <example>&lt;TInternalBase&gt;("INT.") -> INT.TInternal</example>
    /// </param>
    /// <param name="assemblies">assemblies to find types, if not passed - will be used assembly contains TInternalBase</param>
    IIntegrationEventSetup<T> AddInternalEvents<TInternalBase>(string prefix = "", params Assembly[] assemblies)
        where TInternalBase : T;

    /// <summary>
    ///     Add a single event with the routing key, overrides if it already exists (added by
    ///     <see cref="AddInternalEvents&lt;TInternalBase&gt;" />)
    /// </summary>
    IIntegrationEventSetup<T> AddInternalEvent<TEvent>(string routingKey) where TEvent : class, T;

    /// <summary>
    ///     Add multiple events implemented or inherited TExternalBase with the prefix
    /// </summary>
    /// <param name="prefix">
    ///     prefix to add before type name
    ///     <example>&lt;TExternalBase&gt;("SYS.") -> SYS.TExternal</example>
    /// </param>
    /// <param name="assemblies">assemblies to find types, if not passed - will be used assembly contains TExternalBase</param>
    IIntegrationEventSetup<T> AddExternalEvents<TExternalBase>(string prefix, params Assembly[] assemblies) where TExternalBase : T;

    /// <summary>
    ///     Add a single event with the routing key, overrides if it already exists (added by
    ///     <see cref="AddExternalEvents&lt;TExternalBase&gt;" />)
    /// </summary>
    IIntegrationEventSetup<T> AddExternalEvent<TEvent>(string routingKey) where TEvent : class, T;
}
