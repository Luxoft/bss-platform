using System.Reflection;

using Bss.Platform.Events.Abstractions;

using DotNetCore.CAP;
using DotNetCore.CAP.Internal;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bss.Platform.Events.Internal;

public class CapConsumerServiceSelectorLegacy(IServiceProvider serviceProvider, Assembly assembly)
    : CapConsumerServiceSelectorBase(serviceProvider)
{
    protected override IEnumerable<Type> GetInternalEventTypes() =>
        assembly
            .ExportedTypes
            .Where(x => typeof(IIntegrationEvent).IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false });

    protected override CapSubscribeAttribute ProvideCapSubscribeAttribute(Type eventType)
    {
        var subscribeAttribute = new CapSubscribeAttribute(eventType.Name);
        this.SetSubscribeAttribute(subscribeAttribute);
        return subscribeAttribute;
    }
}

public class CapConsumerServiceSelectorNew(IServiceProvider serviceProvider, IEventTypeProvider eventTypeProvider, IOptions<CapOptions> capOptions)
    : CapConsumerServiceSelectorBase(serviceProvider)
{
    private readonly string queueName = capOptions.Value.DefaultGroupName;
    protected override IEnumerable<Type> GetInternalEventTypes() =>
        eventTypeProvider.InputEvents.Keys;

    protected override CapSubscribeAttribute ProvideCapSubscribeAttribute(Type eventType) =>
        new(eventTypeProvider.InputEvents[eventType]) { Group = this.queueName };
}

public abstract class CapConsumerServiceSelectorBase(IServiceProvider serviceProvider)
    : ConsumerServiceSelector(serviceProvider)
{
    protected abstract IEnumerable<Type> GetInternalEventTypes();

    protected abstract CapSubscribeAttribute ProvideCapSubscribeAttribute(Type eventType);

    protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromControllerTypes() =>
        [];

    protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromInterfaceTypes(IServiceProvider provider)
    {
        var namePrefix = provider.GetRequiredService<IOptions<CapOptions>>().Value.TopicNamePrefix;

        return this.GetInternalEventTypes()
            .Select(x => this.CreateExecutorDescriptor(typeof(CapConsumerExecutor<>).MakeGenericType(x), x, namePrefix));
    }


    private ConsumerExecutorDescriptor CreateExecutorDescriptor(Type executor, Type @event, string? namePrefix)
    {
        var subscribeAttribute = this.ProvideCapSubscribeAttribute(@event);

        var methodInfo = executor
            .GetRuntimeMethods()
            .Single(x => x.Name.Contains(nameof(CapConsumerExecutor<>.HandleAsync)));

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
