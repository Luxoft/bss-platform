using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Services;

public class OutputQueuesInitializer(
    IOutputBindingsResolver bindings,
    IOptions<RabbitMqSettings> options,
    IQueueDeclarationPolicy queuePolicy) : IRabbitInitializer
{
    public int Order => 100;
    public bool IsEnabled
    {
        get
        {
            if (bindings.ResolveQueueBindings().Count == 0) return false;
            if (string.IsNullOrWhiteSpace(options.Value.Exchange))
                throw new InvalidOperationException("RabbitMQ:Exchange is required for output queues.");
            return true;
        }
    }

    public Task InitializeAsync(IModel channel, CancellationToken cancellationToken)
    {
        var exchange = options.Value.Exchange;
        if (string.IsNullOrWhiteSpace(exchange)) throw new InvalidOperationException("RabbitMQ:Exchange is required for output queues.");
        channel.ExchangeDeclare(exchange, ExchangeType.Topic, true);
        foreach (var (queue, routes) in bindings.ResolveQueueBindings())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(queue);
            queuePolicy.Declare(channel, queue);
            foreach (var route in routes)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(route);
                channel.QueueBind(queue, exchange, route);
            }
        }

        return Task.CompletedTask;
    }
}
