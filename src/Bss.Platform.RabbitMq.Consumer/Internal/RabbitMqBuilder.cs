using Bss.Platform.RabbitMq.Consumer.Interfaces;

namespace Bss.Platform.RabbitMq.Consumer.Internal;

internal class RabbitMqBuilder<TEvent> : IMessageRegister<TEvent>
{
    public List<MessageLink> RegisteredMessages { get; } = [];

    public IMessageRegister<TEvent> Add<TMessage>(string routingKey)
        where TMessage : TEvent
    {
        this.RegisteredMessages.Add(new(typeof(TMessage), routingKey));
        return this;
    }
}
