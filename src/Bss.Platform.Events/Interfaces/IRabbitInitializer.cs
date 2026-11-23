using RabbitMQ.Client;

namespace Bss.Platform.Events.Interfaces;

/// <summary>
/// Runs one-time RabbitMQ topology setup (queues, bindings, etc.) against a channel rented at startup.<br/>
/// Register additional implementations via <c>services.AddSingleton&lt;IRabbitInitializer, TInitializer&gt;()</c>.
/// </summary>
public interface IRabbitInitializer
{
    Task InitializeAsync(IModel model, CancellationToken cancellationToken);
}
