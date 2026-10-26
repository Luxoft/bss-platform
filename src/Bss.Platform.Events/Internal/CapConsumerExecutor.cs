using Bss.Platform.Events.Interfaces;

namespace Bss.Platform.Events.Internal;

internal class CapConsumerExecutor<TEvent>(IIntegrationEventProcessor<TEvent> eventProcessor)
{
    public Task HandleAsync(TEvent @event, CancellationToken cancellationToken) => eventProcessor.ProcessAsync(@event, cancellationToken);
}
