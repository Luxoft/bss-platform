namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IMessageRegister<in TEvent>
{
    IMessageRegister<TEvent> Add<TMessage>(string routingKey)
        where TMessage : TEvent;
}
