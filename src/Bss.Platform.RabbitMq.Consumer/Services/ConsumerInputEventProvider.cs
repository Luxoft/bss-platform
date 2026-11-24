using Bss.Platform.RabbitMq.Consumer.Settings;
using Bss.Platform.RabbitMq.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bss.Platform.RabbitMq.Consumer.Services;

public class ConsumerInputEventProvider(IServiceProvider services, IOptions<RabbitMqConsumerSettings> options) : IRabbitInputEventProvider
{
    public IReadOnlyDictionary<string, Type> Events => services.GetKeyedService<Dictionary<string, Type>>(DependencyInjection.RoutingMessageProviderKey) ?? [];
    public string? QueueName => options.Value.Queue;
    public string? Exchange => options.Value.Exchange;
    public int? ConnectionAttemptCount => options.Value.ConnectionAttemptCount;
}
