namespace Bss.Platform.RabbitMq.Interfaces;

public interface IRabbitMqEventPublisher
{
    Task PublishAsync(object @event, CancellationToken cancellationToken = default);
}
