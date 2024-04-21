using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IRabbitMqMessageReader
{
    Task ReadAsync(IModel channel, CancellationToken token);
}
