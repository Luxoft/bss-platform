using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

/// <summary>Adapts existing synchronous consumer initializers to the shared startup lifecycle.</summary>
public class ConsumerTopologyInitializer(
    IEnumerable<IRabbitMqInitializer> initializers,
    IOptions<RabbitMqSettings> common,
    IOptions<RabbitMqConsumerSettings> consumer) : IRabbitInitializer
{
    public bool IsEnabled
    {
        get
        {
            if (string.IsNullOrWhiteSpace(common.Value.Exchange) || string.IsNullOrWhiteSpace(consumer.Value.Queue))
                throw new InvalidOperationException("RabbitMQ exchange and consumer queue are required.");
            return true;
        }
    }

    public Task InitializeAsync(IModel channel, CancellationToken cancellationToken)
    {
        foreach (var initializer in initializers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            initializer.Initialize(channel);
        }

        return Task.CompletedTask;
    }
}
