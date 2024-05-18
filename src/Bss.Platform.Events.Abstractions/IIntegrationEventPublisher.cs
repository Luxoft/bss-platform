namespace Bss.Platform.Events.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(IIntegrationEvent @event, CancellationToken cancellationToken);
}
