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
    protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromControllerTypes() => [];

    protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromInterfaceTypes(IServiceProvider provider)
    {
        var namePrefix = provider.GetRequiredService<IOptions<CapOptions>>().Value.TopicNamePrefix;

        return assembly
               .ExportedTypes
               .Where(x => typeof(IIntegrationEvent).IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false })
               .Select(x => this.CreateExecutorDescriptor(typeof(CapConsumerExecutor<>).MakeGenericType(x), x, namePrefix));
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
