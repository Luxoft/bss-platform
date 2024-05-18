using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;

namespace Bss.Platform.Events;

internal class CapConsumerExecutor<TEvent>(IIntegrationEventProcessor eventProcessor)
    where TEvent : IIntegrationEvent
{
    public Task HandleAsync(TEvent @event, CancellationToken cancellationToken) => eventProcessor.ProcessAsync(@event, cancellationToken);
}
