namespace Bss.Platform.RabbitMq.Interfaces;

public interface IRabbitInputEventProvider
{
    IReadOnlyDictionary<string, Type> Events { get; }
    string? QueueName { get; }
    string? Exchange => null;
    int? ConnectionAttemptCount => null;
}
