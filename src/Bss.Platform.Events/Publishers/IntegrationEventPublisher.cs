using Bss.Platform.Events.Abstractions;

using DotNetCore.CAP;

namespace Bss.Platform.Events.Publishers;

public class IntegrationEventPublisher(ICapPublisher capPublisher, ICapTransaction capTransaction) : IIntegrationEventPublisher
{
    public Task PublishAsync(IIntegrationEvent @event, CancellationToken cancellationToken)
    {
        if (capPublisher.Transaction is not null && capPublisher.Transaction != capTransaction)
        {
            throw new Exception("There cannot be different CAP transactions within the same scope");
        }

        capPublisher.Transaction = capTransaction;
        return capPublisher.PublishAsync(@event.GetType().Name, @event, cancellationToken: cancellationToken);
    }
}
