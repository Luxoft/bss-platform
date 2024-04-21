using Bss.Platform.RabbitMq.Consumer.Enums;

namespace Bss.Platform.RabbitMq.Consumer.Settings;

public class RabbitMqConsumerSettings
{
    public int ReceiveMessageDelayMilliseconds { get; set; } = 1000;

    public int RejectMessageDelayMilliseconds { get; set; } = 3000;

    public int FailedMessageRetryCount { get; set; } = 3;

    public int? ConnectionAttemptCount { get; set; }

    public string Exchange { get; set; } = default!;

    public string Queue { get; set; } = default!;

    public string[] RoutingKeys { get; set; } = [];

    public string DeadLetterExchange { get; set; } = "deadletters";

    public ConsumerMode Mode { get; set; } = ConsumerMode.MultipleActiveConsumers;

    /// <summary>
    ///     for single active consumer mode - how often should consumer try to become active
    /// </summary>
    public int InactiveConsumerSleepMilliseconds { get; set; } = 60 * 1000;

    /// <summary>
    ///     for single active consumer mode - how often should consumer update its active status
    /// </summary>
    public int ActiveConsumerRefreshMilliseconds { get; set; } = 3 * 60 * 1000;
}
