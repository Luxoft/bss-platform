using System.Text.Json;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IRabbitMqMessageProcessor
{
    Task ProcessAsync(IBasicProperties properties, string routingKey, string message, CancellationToken token);

    protected static readonly JsonSerializerOptions CaseInsensitiveJsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
}
