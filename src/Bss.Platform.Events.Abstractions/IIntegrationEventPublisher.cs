namespace Bss.Platform.Events.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(object @event, CancellationToken cancellationToken);
}
