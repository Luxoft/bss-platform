using Bss.Platform.Events.Interfaces;
using Bss.Platform.Events.Models;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.Events.Internal;

internal sealed class ExternalSystemQueueBindingsInitializer(
    IExternalSystemBindingsResolver bindingsResolver,
    IOptions<RabbitIntegrationEventsOptions> eventOptions) : IRabbitInitializer
{
    public Task InitializeAsync(IModel model, CancellationToken cancellationToken)
    {
        var exchangeName = eventOptions.Value.MessageQueue.ExchangeName;
        foreach (var (queue, routingKeys) in bindingsResolver.ResolveQueueBindings())
        {
            cancellationToken.ThrowIfCancellationRequested();

            model.QueueDeclare(queue, true, false, false, null);

            foreach (var routingKey in routingKeys.Distinct())
            {
                model.QueueBind(queue, exchangeName, routingKey);
            }
        }

        return Task.CompletedTask;
    }
}
