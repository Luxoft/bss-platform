using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;
using Bss.Platform.Events.Models;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.Events.Internal;

internal sealed class ExternalSystemQueueBindingsInitializer(
    IEventTypeProvider eventTypeProvider,
    IOptions<IntegrationEventsOptions> eventOptions,
    IOptions<ExternalSystemBindingsOptions> bindingsOptions) : IRabbitInitializer
{
    public Task InitializeAsync(IModel model, CancellationToken cancellationToken)
    {
        var options = bindingsOptions.Value;
        if (options.SystemBindings.Count == 0)
        {
            return Task.CompletedTask;
        }

        var exchangeName = eventOptions.Value.MessageQueue.ExchangeName;
        foreach (var (queue, routingKeys) in this.ResolveBindings(options))
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

    /// <summary>
    /// Resolves which output-event routing keys each configured queue should be bound to.<br/>
    /// <c>null</c>/empty <see cref="ExternalSystemBindingsOptions.SystemBindings"/> values fall back to the default
    /// set: all <see cref="IEventTypeProvider.OutputEvents"/> except those matching <see cref="ExternalSystemBindingsOptions.ExcludeOutputEvents"/>.
    /// </summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveBindings(ExternalSystemBindingsOptions options)
    {
        var defaultRoutingKeys = eventTypeProvider.OutputEvents.Values
            .Distinct()
            .Where(routingKey => !options.ExcludeOutputEvents.Any(pattern => WildcardMatcher.IsMatch(routingKey, pattern)))
            .ToArray();

        return options.SystemBindings.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<string>)(x.Value is { Length: > 0 } explicitRoutingKeys ? explicitRoutingKeys : defaultRoutingKeys));
    }
}
