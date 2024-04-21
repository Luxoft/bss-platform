using Bss.Platform.RabbitMq.Consumer.Interfaces;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal record ConcurrentConsumer(IRabbitMqMessageReader MessageReader) : IRabbitMqConsumer
{
    public async Task ConsumeAsync(IModel channel, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await this.MessageReader.ReadAsync(channel, token);
        }
    }

    public void Dispose()
    {
    }
}
