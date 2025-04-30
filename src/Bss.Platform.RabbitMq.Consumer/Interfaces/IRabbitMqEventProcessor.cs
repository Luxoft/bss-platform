namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IRabbitMqEventProcessor<in TEvent>
{
    Task ProcessAsync(TEvent @event, CancellationToken token);
}
