using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Schemas;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Services;

public class ExportSchemasInitializer(
    IRabbitSchemaTypeProvider types,
    IRabbitSchemaSender sender,
    IOptions<RabbitSchemaOptions> schemas,
    IOptions<RabbitMqSettings> common,
    IRabbitInputEventProvider? inputs = null,
    ILogger<ExportSchemasInitializer>? logger = null) : IRabbitInitializer
{
    public int Order => 1000;
    public bool IsEnabled
    {
        get
        {
            try
            {
                if (!schemas.Value.Enabled || !types.HasContracts) return false;
                if (string.IsNullOrWhiteSpace(common.Value.Exchange))
                    throw new InvalidOperationException("RabbitMQ:Exchange is required for schema export.");
                return true;
            }
            catch (Exception exception)
            {
                this.LogFailure(exception);
                return false;
            }
        }
    }

    private RabbitSchemaSource Source => new(common.Value.Exchange!, schemas.Value.SourceQueueName ?? inputs?.QueueName);
    public Task InitializeAsync(IModel channel, CancellationToken cancellationToken) =>
        this.HandleAsync(() => sender.SendAsync(channel, types, this.Source, cancellationToken), cancellationToken);

    public Task ExecuteAsync(Func<CancellationToken, Task<IModel>> createChannel, CancellationToken cancellationToken) =>
        this.HandleAsync(() => sender.SendAsync(createChannel, types, this.Source, cancellationToken), cancellationToken);

    private async Task HandleAsync(Func<Task> send, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await send();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this.LogFailure(exception);
        }
    }

    private void LogFailure(Exception exception) =>
        logger?.LogError(exception, "Failed to export RabbitMQ event schemas");
}
