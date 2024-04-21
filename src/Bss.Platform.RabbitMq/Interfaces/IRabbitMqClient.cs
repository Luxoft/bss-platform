using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Interfaces;

public interface IRabbitMqClient
{
    Task<IConnection?> TryConnectAsync(int? attempts, CancellationToken token = default);
}
