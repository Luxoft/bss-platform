namespace Bss.Platform.RabbitMq.Consumer.Enums;

public enum DeadLetterDecision
{
    Requeue = 1,

    RemoveFromQueue = 2
}
