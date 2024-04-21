using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IRabbitMqConsumer : IDisposable
{
    Task ConsumeAsync(IModel channel, CancellationToken token);
}
