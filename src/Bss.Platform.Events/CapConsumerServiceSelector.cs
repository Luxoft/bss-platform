using System.Reflection;

using Bss.Platform.Events.Abstractions;

using DotNetCore.CAP;
using DotNetCore.CAP.Internal;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bss.Platform.Events;

public class CapConsumerServiceSelector(IServiceProvider serviceProvider, Assembly assembly)
    : ConsumerServiceSelector(serviceProvider)
{
    protected virtual Type EventBaseType => typeof(IIntegrationEvent);

    protected virtual Type GetExecutorType(Type eventType) => typeof(CapConsumerExecutor<>).MakeGenericType(eventType);

    protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromControllerTypes() => [];

    protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromInterfaceTypes(IServiceProvider provider)
    {
        var namePrefix = provider.GetRequiredService<IOptions<CapOptions>>().Value.TopicNamePrefix;

        return assembly
               .ExportedTypes
               .Where(x => this.EventBaseType.IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false, ContainsGenericParameters: false })
               .Select(x => this.CreateExecutorDescriptor(this.GetExecutorType(x), x, namePrefix));
    }

    private ConsumerExecutorDescriptor CreateExecutorDescriptor(Type executor, Type @event, string? namePrefix)
    {
        var subscribeAttribute = new CapSubscribeAttribute(@event.Name);
        this.SetSubscribeAttribute(subscribeAttribute);

        var methodInfo = executor
                         .GetRuntimeMethods()
                         .Single(x => x.Name.Contains(nameof(CapConsumerExecutor<IIntegrationEvent>.HandleAsync)));

        var methodParameters = methodInfo.GetParameters();
        return new ConsumerExecutorDescriptor
               {
                   Attribute = subscribeAttribute,
                   ClassAttribute = null,
                   MethodInfo = methodInfo,
                   ImplTypeInfo = executor.GetTypeInfo(),
                   ServiceTypeInfo = null,
                   TopicNamePrefix = namePrefix,
                   Parameters = new List<ParameterDescriptor>
                                {
                                    new() { ParameterType = methodParameters[0].ParameterType, IsFromCap = false },
                                    new() { ParameterType = methodParameters[1].ParameterType, IsFromCap = true }
                                }
               };
    }
}

public class CapConsumerServiceSelector<TEvent>(IServiceProvider serviceProvider, Assembly assembly)
    : CapConsumerServiceSelector(serviceProvider, assembly)
{
    protected override Type EventBaseType => typeof(TEvent);

    protected override Type GetExecutorType(Type eventType) => typeof(CapConsumerExecutor<,>).MakeGenericType(eventType, typeof(TEvent));
}
