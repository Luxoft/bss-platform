using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IRabbitMqInitializer
{
    void Initialize(IModel model);
}
