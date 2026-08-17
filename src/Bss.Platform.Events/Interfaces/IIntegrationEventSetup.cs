using System.Reflection;

namespace Bss.Platform.Events.Interfaces;

public interface IIntegrationEventSetup<in TIn, in TOut>
{
    /// <summary>
    ///     Add multiple events implemented or inherited TInternalBase with the prefix
    /// </summary>
    /// <param name="prefix">
    ///     prefix to add before type name
    ///     <example>&lt;TInternalBase&gt;("INT.") -> INT.TInternal</example>
    /// </param>
    /// <param name="assemblies">assemblies to find types, if not passed - will be used assembly contains TInternalBase</param>
    IIntegrationEventSetup<TIn, TOut> AddInputEvents<TInputBase>(string prefix = "", params Assembly[] assemblies)
        where TInputBase : TIn;

    /// <summary>
    ///     Add a single event with the routing key, overrides if it already exists (added by
    ///     <see cref="AddInputEvents{TInputBase}" />)
    /// </summary>
    IIntegrationEventSetup<TIn, TOut> AddInputEvent<TInput>(string routingKey) where TInput : class, TIn;

    /// <summary>
    ///     Add multiple events implemented or inherited TExternalBase with the prefix
    /// </summary>
    /// <param name="prefix">
    ///     prefix to add before type name
    ///     <example>&lt;TExternalBase&gt;("SYS.") -> SYS.TExternal</example>
    /// </param>
    /// <param name="assemblies">assemblies to find types, if not passed - will be used assembly contains TExternalBase</param>
    IIntegrationEventSetup<TIn, TOut> AddOutputEvents<TOutputBase>(string prefix, params Assembly[] assemblies) where TOutputBase : TOut;

    /// <summary>
    ///     Add a single event with the routing key, overrides if it already exists (added by
    ///     <see cref="AddOutputEvents{TOutputBase}" />)
    /// </summary>
    IIntegrationEventSetup<TIn, TOut> AddOutputEvent<TOutput>(string routingKey) where TOutput : class, TOut;

    bool IsAssignableAndSatisfyCondition<TAssignableTo>(TypeInfo typeInfo);
}
