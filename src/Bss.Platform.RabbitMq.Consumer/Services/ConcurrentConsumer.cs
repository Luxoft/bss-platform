using Bss.Platform.RabbitMq.Consumer.Interfaces;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal class ConcurrentConsumer(IRabbitMqMessageReader messageReader) : IRabbitMqConsumer
{
    public async Task ConsumeAsync(IModel channel, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await messageReader.ReadAsync(channel, token);
        }
    }

    public void Dispose()
    {
    }
}
