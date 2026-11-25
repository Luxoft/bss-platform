using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Schemas;

public interface IRabbitSchemaSender
{
    Task SendAsync(IModel channel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken cancellationToken = default);

    async Task SendAsync(Func<CancellationToken, Task<IModel>> createChannel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken cancellationToken = default)
    {
        using var channel = await createChannel(cancellationToken);
        await this.SendAsync(channel, types, source, cancellationToken);
    }
}
