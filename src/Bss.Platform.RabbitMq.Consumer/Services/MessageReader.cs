using System.Text;

using Bss.Platform.RabbitMq.Consumer.Enums;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal record MessageReader(
    IRabbitMqMessageProcessor MessageProcessor,
    IDeadLetterProcessor DeadLetterProcessor,
    ILogger<MessageReader> Logger,
    IOptions<RabbitMqConsumerSettings> ConsumerSettings)
    : IRabbitMqMessageReader
{
    public async Task ReadAsync(IModel channel, CancellationToken token)
    {
        var result = channel.BasicGet(this.ConsumerSettings.Value.Queue, false);
        if (result is null)
        {
            await Delay(this.ConsumerSettings.Value.ReceiveMessageDelayMilliseconds, token);
            return;
        }

        await this.ProcessAsync(result, channel, token);
    }

    private async Task ProcessAsync(BasicGetResult message, IModel channel, CancellationToken token)
    {
        var result = await Policy
                           .Handle<Exception>()
                           .WaitAndRetryAsync(
                               this.ConsumerSettings.Value.FailedMessageRetryCount,
                               _ => TimeSpan.FromMilliseconds(this.ConsumerSettings.Value.RejectMessageDelayMilliseconds))
                           .ExecuteAndCaptureAsync(
                               innerToken => this.MessageProcessor.ProcessAsync(
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
                var deadLetteringResult = await this.DeadLetterProcessor.ProcessAsync(
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
            this.Logger.LogError(ex, "Failed to deadLetter message with routing key {RoutingKey}", message.RoutingKey);

            await Delay(this.ConsumerSettings.Value.ReceiveMessageDelayMilliseconds, token);

            channel.BasicNack(message.DeliveryTag, false, true);
        }
    }

    private static Task Delay(int value, CancellationToken token) => Task.Delay(TimeSpan.FromMilliseconds(value), token);
}
