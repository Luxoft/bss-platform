using System.Text;

using Bss.Platform.RabbitMq.Consumer.Enums;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;
using Bss.Platform.RabbitMq.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal record DeadLetterProcessor(
    IRabbitMqClient Client,
    ILogger<DeadLetterProcessor> Logger,
    IOptions<RabbitMqConsumerSettings> ConsumerSettings)
    : IDeadLetterProcessor
{
    public async Task<DeadLetterDecision> ProcessAsync(string message, string routingKey, Exception? exception, CancellationToken token)
    {
        try
        {
            using var connection = await this.Client.TryConnectAsync(this.ConsumerSettings.Value.ConnectionAttemptCount, token);
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
                                     { "queue", this.ConsumerSettings.Value.Queue },
                                     { "error", exception?.GetBaseException().Message ?? "unknown exception" },
                                     { "stacktrace", exception?.StackTrace ?? "missing stacktrace" }
                                 };

            channel.BasicPublish(this.ConsumerSettings.Value.DeadLetterExchange, string.Empty, properties, Encoding.UTF8.GetBytes(message));
            return DeadLetterDecision.RemoveFromQueue;
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to process dead letter with routing key '{RoutingKey}'", routingKey);
            return DeadLetterDecision.Requeue;
        }
    }
}
