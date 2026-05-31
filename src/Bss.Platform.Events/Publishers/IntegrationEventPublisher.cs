using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;

using DotNetCore.CAP;

namespace Bss.Platform.Events.Publishers;

public class IntegrationEventPublisherLegacy(ICapPublisher capPublisher, ICapTransaction capTransaction)
    : IntegrationEventPublisherBase<IIntegrationEvent>(capPublisher, capTransaction), IIntegrationEventPublisher
{
    private readonly ICapPublisher capPublisher = capPublisher;

    protected override Task PublishInternalAsync(IIntegrationEvent @event, CancellationToken cancellationToken) =>
        this.capPublisher.PublishAsync(@event.GetType().Name, @event, cancellationToken: cancellationToken);
}

public class IntegrationEventPublisherNew<T>(ICapPublisher capPublisher, ICapTransaction capTransaction, IEventTypeProvider eventTypeProvider)
    : IntegrationEventPublisherBase<T>(capPublisher, capTransaction)
    where T: notnull
{
    private readonly ICapPublisher capPublisher = capPublisher;

    protected override async Task PublishInternalAsync(T @event, CancellationToken cancellationToken)
    {
        if (eventTypeProvider.InternalEvents.TryGetValue(@event.GetType(), out var internalRoutingKey))
        {
            await this.capPublisher.PublishAsync(internalRoutingKey, @event, cancellationToken: cancellationToken);
        }

        if (eventTypeProvider.ExternalEvents.TryGetValue(@event.GetType(), out var externalRoutingKey)
            && externalRoutingKey != internalRoutingKey)
        {
            await this.capPublisher.PublishAsync(externalRoutingKey, @event, cancellationToken: cancellationToken);
        }

        if (internalRoutingKey == null && externalRoutingKey == null)
        {
            throw new($"No routing key found for event type {@event.GetType().FullName}");
        }
    }
}

public abstract class IntegrationEventPublisherBase<T>(ICapPublisher capPublisher, ICapTransaction capTransaction)
    : IIntegrationEventPublisher<T>
{
    public Task PublishAsync(T @event, CancellationToken cancellationToken)
    {
        if (capPublisher.Transaction is not null && capPublisher.Transaction != capTransaction)
        {
            throw new("There cannot be different CAP transactions within the same scope");
        }

        capPublisher.Transaction = capTransaction;
        return this.PublishInternalAsync(@event, cancellationToken);
    }

    protected abstract Task PublishInternalAsync(T @event, CancellationToken cancellationToken);
}
