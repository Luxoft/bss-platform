using System.Text;

using Bss.Platform.RabbitMq.Consumer.Enums;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal class MessageReader(
    IRabbitMqMessageProcessor messageProcessor,
    IDeadLetterProcessor deadLetterProcessor,
    ILogger<MessageReader> logger,
    IOptions<RabbitMqConsumerSettings> consumerSettings)
    : IRabbitMqMessageReader
{
    public async Task ReadAsync(IModel channel, CancellationToken token)
    {
        var result = channel.BasicGet(consumerSettings.Value.Queue, false);
        if (result is null)
        {
            await Delay(consumerSettings.Value.ReceiveMessageDelayMilliseconds, token);
            return;
        }

        await this.ProcessAsync(result, channel, token);
    }

    private async Task ProcessAsync(BasicGetResult message, IModel channel, CancellationToken token)
    {
        var result = await Policy
                           .Handle<Exception>()
                           .WaitAndRetryAsync(
                               consumerSettings.Value.FailedMessageRetryCount,
                               _ => TimeSpan.FromMilliseconds(consumerSettings.Value.RejectMessageDelayMilliseconds))
                           .ExecuteAndCaptureAsync(
                               innerToken => messageProcessor.ProcessAsync(
                                   message.BasicProperties,
                                   message.RoutingKey,
                                   GetMessageBody(message),
                                   innerToken),
                               token);

        await this.HandleProcessResultAsync(message, channel, result, token);
    }

    private static string GetMessageBody(BasicGetResult message) => Encoding.UTF8.GetString(message.Body.Span);

    private async Task HandleProcessResultAsync(BasicGetResult message, IModel channel, PolicyResult result, CancellationToken token)
    {
        try
        {
            if (result.Outcome == OutcomeType.Successful)
            {
                channel.BasicAck(message.DeliveryTag, false);
            }
            else
            {
                var deadLetteringResult = await deadLetterProcessor.ProcessAsync(
                                              GetMessageBody(message),
                                              message.RoutingKey,
                                              result.FinalException,
                                              token);
                if (deadLetteringResult == DeadLetterDecision.RemoveFromQueue)
                {
                    channel.BasicAck(message.DeliveryTag, false);
                }
                else
                {
                    channel.BasicNack(message.DeliveryTag, false, true);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deadLetter message with routing key {RoutingKey}", message.RoutingKey);

            await Delay(consumerSettings.Value.ReceiveMessageDelayMilliseconds, token);

            channel.BasicNack(message.DeliveryTag, false, true);
        }
    }

    private static Task Delay(int value, CancellationToken token) => Task.Delay(TimeSpan.FromMilliseconds(value), token);
}
