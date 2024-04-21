using Bss.Platform.RabbitMq.Consumer.Enums;

namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IDeadLetterProcessor
{
    Task<DeadLetterDecision> ProcessAsync(string message, string routingKey, Exception? exception, CancellationToken token);
}
