using Bss.Platform.Events.Abstractions;

namespace Bss.Platform.Events.Interfaces;

public interface IIntegrationEventProcessor : IIntegrationEventProcessor<IIntegrationEvent>;

public interface IIntegrationEventProcessor<in T>
{
    Task ProcessAsync(T @event, CancellationToken token);
}
