using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal class ConsumerInitializer(IOptions<RabbitMqConsumerSettings> options) : IRabbitMqInitializer
{
    public void Initialize(IModel model)
    {
        var consumerSettings = options.Value;

        model.ExchangeDeclare(consumerSettings.Exchange, ExchangeType.Topic, true);
        model.ExchangeDeclare(consumerSettings.DeadLetterExchange, ExchangeType.Fanout, true);

        model.QueueDeclare(consumerSettings.Queue, true, false, false, null);

        if (consumerSettings.RoutingKeys.Length == 0)
        {
            model.QueueBind(consumerSettings.Queue, consumerSettings.Exchange, "#");
            return;
        }

        foreach (var routingKey in consumerSettings.RoutingKeys)
        {
            model.QueueBind(consumerSettings.Queue, consumerSettings.Exchange, routingKey);
        }
    }
}
