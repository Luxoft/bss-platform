using System.Text;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Models;

using DotNetCore.CAP;
using DotNetCore.CAP.RabbitMQ;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.Events.Internal;

internal sealed partial class DeadLetterProcessor(
    IConnectionChannelPool connectionChannelPool,
    IOptions<CapOptions> capOptions,
    IEventTypeProvider registeredTypes,
    IOptions<IntegrationEventsOptions> eventOptions,
    ILogger<DeadLetterProcessor> logger) : IFailedEventProcessor<object>
{
    private readonly string exchangeName = eventOptions.Value.DeadLetterOptions.ExchangeName;
    private readonly string originMessageQueueName = capOptions.Value.DefaultGroupName;

    public Task HandleAsync(object? value, Exception ex, string? rawMessageBody)
    {
        var routingKey = value != null && registeredTypes.InputEvents.TryGetValue(value.GetType(), out var registeredRoutingKey)
            ? registeredRoutingKey
            : $"unknown message: {value?.GetType().Name ?? "<empty value message>"}";

        this.SendDeadLetter(routingKey, ex, rawMessageBody);
        return Task.CompletedTask;
    }

    internal void SendDeadLetter(string routingKey, Exception ex, string? rawMessageBody)
    {
        try
        {
            using var channel = connectionChannelPool.Rent();

            var props = channel.CreateBasicProperties();
            props.DeliveryMode = 2;

            props.Headers = new Dictionary<string, object>
            {
                ["error"] = ex.GetBaseException().Message,
                ["queue"] = this.originMessageQueueName,
                ["routingKey"] = routingKey,
                ["stacktrace"] = ex.StackTrace ?? string.Empty
            };

            channel.BasicPublish(this.exchangeName, string.Empty, props, Encoding.UTF8.GetBytes(rawMessageBody ?? string.Empty));

            if (channel.NextPublishSeqNo > 0)
            {
                channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception exception)
        {
            this.LogError(routingKey, exception.GetType().Name, exception.Message);
        }
    }

    [LoggerMessage(LogLevel.Error, Message = "Fail send deadletter {RoutingKey}, {ErrorType}, {ErrorText}")]
    partial void LogError(string routingKey, string errorType, string errorText);
}
