using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Consumer.Interfaces;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

/// <summary>Adapts existing synchronous consumer initializers to the shared startup lifecycle.</summary>
public class ConsumerTopologyInitializer(IEnumerable<IRabbitMqInitializer> initializers) : IRabbitInitializer
{
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
