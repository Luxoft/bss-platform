using System.Reflection;

namespace Bss.Platform.Events.Interfaces;

public interface IIntegrationEventSetup<in T>
{
    /// <summary>
    ///     Add a single internal event with the routing key, overrides if it already exists (added by default)
    /// </summary>
    IIntegrationEventSetup<T> AddInternalEvent<TImpl>(string routingKey) where TImpl : T;

    /// <summary>
    ///     Add multiple events implemented or inherited TExternalBase with the prefix
    /// </summary>
    /// <param name="prefix">
    ///     prefix to add before type name
    ///     <example>&lt;TExternalBase&gt;("SYS.") -> SYS.TExternal</example>
    /// </param>
    /// <param name="assemblies">assemblies to find types, if not passed - will be used assembly contains TExternalBase</param>
    IIntegrationEventSetup<T> AddExternalEvents<TExternalBase>(string prefix, params Assembly[] assemblies);

    /// <summary>
    ///     Add a single event with the routing key, overrides if it already exists (added by
    ///     <see cref="AddExternalEvents&lt;TExternalBase&gt;" />)
    /// </summary>
    IIntegrationEventSetup<T> AddExternalEvent<TExternalConcrete>(string routingKey) where TExternalConcrete : class;
}
