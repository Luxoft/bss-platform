namespace Bss.Platform.Events.Abstractions;

public interface IIntegrationEventPublisher<in T>
{
    Task PublishAsync(T @event, CancellationToken cancellationToken);
}

public interface IIntegrationEventPublisher : IIntegrationEventPublisher<IIntegrationEvent>;
