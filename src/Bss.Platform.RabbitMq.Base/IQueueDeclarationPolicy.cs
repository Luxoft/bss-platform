using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Base;

public interface IQueueDeclarationPolicy
{
    void Declare(IModel channel, string queue);
}

public class QuorumQueueDeclarationPolicy : IQueueDeclarationPolicy
{
    public virtual void Declare(IModel channel, string queue) => channel.QueueDeclare(
        queue, true, false, false, new Dictionary<string, object> { ["x-queue-type"] = "quorum" });
}
