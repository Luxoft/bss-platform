using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IRabbitMqMessageProcessor
{
    Task ProcessAsync(IBasicProperties properties, string routingKey, string message, CancellationToken token);
}
