using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Base;

public interface IRabbitInitializer
{
    int Order => 0;
    bool IsEnabled => true;
    Task InitializeAsync(IModel channel, CancellationToken cancellationToken);

    async Task ExecuteAsync(Func<CancellationToken, Task<IModel>> createChannel, CancellationToken cancellationToken)
    {
        using var channel = await createChannel(cancellationToken);
        await this.InitializeAsync(channel, cancellationToken);
    }
}
