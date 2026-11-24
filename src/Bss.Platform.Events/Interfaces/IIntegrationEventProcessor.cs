using Bss.Platform.Events.Abstractions;

namespace Bss.Platform.Events.Interfaces;

public interface IIntegrationEventProcessor : IIntegrationEventProcessor<IIntegrationEvent>;

public interface IIntegrationEventProcessor<in TEvent>
{
    Task ProcessAsync(TEvent @event, CancellationToken token);
}
