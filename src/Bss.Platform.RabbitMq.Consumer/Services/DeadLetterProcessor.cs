using System.Text;

using Bss.Platform.RabbitMq.Consumer.Enums;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;
using Bss.Platform.RabbitMq.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal class DeadLetterProcessor(
    IRabbitMqClient client,
    ILogger<DeadLetterProcessor> logger,
    IOptions<RabbitMqConsumerSettings> consumerSettings)
    : IDeadLetterProcessor
{
    public async Task<DeadLetterDecision> ProcessAsync(string message, string routingKey, Exception? exception, CancellationToken token)
    {
        try
        {
            using var connection = await client.TryConnectAsync(consumerSettings.Value.ConnectionAttemptCount, token);
            if (connection == null)
            {
                throw new Exception("Failed to open connection");
            }

            using var channel = connection.CreateModel();

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.Headers = new Dictionary<string, object>
                                 {
                                     { "routingKey", routingKey },
                                     { "queue", consumerSettings.Value.Queue },
                                     { "error", exception?.GetBaseException().Message ?? "unknown exception" },
                                     { "stacktrace", exception?.StackTrace ?? "missing stacktrace" }
                                 };

            channel.BasicPublish(consumerSettings.Value.DeadLetterExchange, string.Empty, properties, Encoding.UTF8.GetBytes(message));
            return DeadLetterDecision.RemoveFromQueue;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to process dead letter with routing key '{RoutingKey}'", routingKey);
            return DeadLetterDecision.Requeue;
        }
    }
}
