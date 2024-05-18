using Bss.Platform.Events.Abstractions;

namespace Bss.Platform.Events.Interfaces;

public interface IIntegrationEventProcessor
{
    Task ProcessAsync(IIntegrationEvent @event, CancellationToken token);
}
