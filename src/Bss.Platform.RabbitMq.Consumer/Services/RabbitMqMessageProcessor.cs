using System.Text.Json;

using Bss.Platform.RabbitMq.Consumer.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal class RabbitMqMessageProcessor<TEvent>(
    IRabbitMqEventProcessor<TEvent> rabbitEventProcessor,
    ILogger<RabbitMqMessageProcessor<TEvent>> logger,
    [FromKeyedServices(DependencyInjection.RoutingMessageProviderKey)]
    Dictionary<string, Type> registeredHandlers) : IRabbitMqMessageProcessor
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public Task ProcessAsync(IBasicProperties properties, string routingKey, string message, CancellationToken token)
    {
        if (!registeredHandlers.TryGetValue(routingKey, out var handlerType))
        {
            const string error = "The message with routing key '{RoutingKey}' has no registered event.";
            logger.LogError(error, routingKey);
            throw new InvalidOperationException(error.Replace("{RoutingKey}", routingKey));
        }

        var request = JsonSerializer.Deserialize(message, handlerType, CaseInsensitiveJsonSerializerOptions);
        if (request is null)
        {
            const string error = "The request with routing key '{RoutingKey}' could not be deserialized.";
            logger.LogError(error, routingKey);
            throw new InvalidOperationException(error.Replace("{RoutingKey}", routingKey));
        }

        if (request is not TEvent @event)
        {
            var typeName = typeof(TEvent).Name;
            const string error = "The request with routing key '{RoutingKey}' is not satisfied type '{Type}'.";
            logger.LogError(error, routingKey, typeName);
            throw new InvalidOperationException(error.Replace("{RoutingKey}", routingKey).Replace("{Type}", typeName));
        }

        return rabbitEventProcessor.ProcessAsync(@event, token);
    }
}
