using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Schemas;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Services;

public class ExportSchemasInitializer(
    IRabbitSchemaTypeProvider types,
    IRabbitSchemaSender sender,
    IOptions<RabbitSchemaOptions> schemas,
    IOptions<RabbitMqSettings> common,
    IRabbitInputEventProvider? inputs = null) : IRabbitInitializer
{
    public int Order => 1000;
    public bool IsEnabled
    {
        get
        {
            if (!schemas.Value.Enabled || !types.HasContracts) return false;
            if (string.IsNullOrWhiteSpace(common.Value.Exchange))
                throw new InvalidOperationException("RabbitMQ:Exchange is required for schema export.");
            return true;
        }
    }

    private RabbitSchemaSource Source => new(common.Value.Exchange!, schemas.Value.SourceQueueName ?? inputs?.QueueName);
    public Task InitializeAsync(IModel channel, CancellationToken cancellationToken) => sender.SendAsync(channel, types, this.Source, cancellationToken);
    public async Task ExecuteAsync(Func<CancellationToken, Task<IModel>> createChannel, CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(createChannel, types, this.Source, cancellationToken);
        }
        catch (RabbitMqStartupConnectionException exception)
        {
            // The sender elected to propagate the failure under its own policy.
            throw new InvalidOperationException(exception.Message, exception);
        }
    }
}
