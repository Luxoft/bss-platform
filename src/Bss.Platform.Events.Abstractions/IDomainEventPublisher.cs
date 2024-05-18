namespace Bss.Platform.Events.Abstractions;

public interface IDomainEventPublisher
{
    Task PublishAsync(IDomainEvent @event, CancellationToken cancellationToken);
}
