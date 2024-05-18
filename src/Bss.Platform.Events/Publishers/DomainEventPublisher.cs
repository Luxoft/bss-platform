using Bss.Platform.Events.Abstractions;

using MediatR;

namespace Bss.Platform.Events.Publishers;

public class DomainEventPublisher(IMediator mediator) : IDomainEventPublisher
{
    public Task PublishAsync(IDomainEvent @event, CancellationToken cancellationToken) => mediator.Publish(@event, cancellationToken);
}
