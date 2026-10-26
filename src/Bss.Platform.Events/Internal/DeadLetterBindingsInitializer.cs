using Bss.Platform.Events.Interfaces;

using RabbitMQ.Client;

namespace Bss.Platform.Events.Internal;

internal sealed class DeadLetterBindingsInitializer(string exchange, string queue) : IRabbitInitializer
{
    public Task InitializeAsync(IModel model, CancellationToken cancellationToken)
    {
        model.ExchangeDeclare(exchange, ExchangeType.Fanout, true);
        model.QueueDeclare(queue, true, false, false, null);
        model.QueueBind(queue, exchange, string.Empty);
        return Task.CompletedTask;
    }
}
