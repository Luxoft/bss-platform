using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Settings;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

public class ConsumerInitializer(
    IOptions<RabbitMqConsumerSettings> options,
    IOptions<RabbitMqSettings> commonOptions,
    IQueueDeclarationPolicy queuePolicy) : IRabbitMqInitializer
{
    public void Initialize(IModel model)
    {
        var consumerSettings = options.Value;

        var exchange = commonOptions.Value.Exchange;
        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new InvalidOperationException("RabbitMQ:Exchange is required for consumer initialization.");
        }

        model.ExchangeDeclare(exchange, ExchangeType.Topic, true);
        model.ExchangeDeclare(consumerSettings.DeadLetterExchange, ExchangeType.Fanout, true);
        if (!string.IsNullOrWhiteSpace(consumerSettings.DeadLetterQueue))
        {
            queuePolicy.Declare(model, consumerSettings.DeadLetterQueue);
            model.QueueBind(consumerSettings.DeadLetterQueue, consumerSettings.DeadLetterExchange, string.Empty);
        }

        queuePolicy.Declare(model, consumerSettings.Queue);

        if (consumerSettings.RoutingKeys.Length == 0)
        {
            model.QueueBind(consumerSettings.Queue, exchange, "#");
            return;
        }

        foreach (var routingKey in consumerSettings.RoutingKeys)
        {
            model.QueueBind(consumerSettings.Queue, exchange, routingKey);
        }
    }
}
