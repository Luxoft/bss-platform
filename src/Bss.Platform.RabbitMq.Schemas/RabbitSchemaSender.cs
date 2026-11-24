using System.Text.Json;

using Bss.Platform.RabbitMq.Base;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Schemas;

public class RabbitSchemaSender(
    IOptions<RabbitSchemaOptions> options,
    IQueueDeclarationPolicy queuePolicy,
    RabbitSchemaMessageBuilder messageBuilder,
    ILogger<RabbitSchemaSender>? logger = null) : IRabbitSchemaSender
{
    protected virtual string DestinationQueue => options.Value.QueueName;

    public virtual Task SendAsync(IModel channel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken cancellationToken = default) =>
        this.HandleAsync(() =>
        {
            this.Publish(channel, types, source, cancellationToken);
            return Task.CompletedTask;
        }, source, cancellationToken);

    public virtual Task SendAsync(Func<CancellationToken, Task<IModel>> createChannel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken cancellationToken = default) =>
        this.HandleAsync(async () =>
        {
            using var channel = await createChannel(cancellationToken);
            this.Publish(channel, types, source, cancellationToken);
        }, source, cancellationToken);

    private async Task HandleAsync(Func<Task> send, RabbitSchemaSource source, CancellationToken token)
    {
        var settings = options.Value; // Validation errors must not be suppressed.
        if (!settings.Enabled) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Exchange);
        token.ThrowIfCancellationRequested();
        try { await send(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Failed to publish RabbitMQ event schemas to {Exchange}", source.Exchange);
            if (settings.ThrowOnFailure) throw;
        }
    }

    private void Publish(IModel channel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken token)
    {
        var settings = options.Value;
        var body = JsonSerializer.SerializeToUtf8Bytes(messageBuilder.Build(types, source, settings.SystemName));
        token.ThrowIfCancellationRequested();
        channel.ExchangeDeclare(source.Exchange, ExchangeType.Topic, true);
        queuePolicy.Declare(channel, this.DestinationQueue);
        channel.QueueBind(this.DestinationQueue, source.Exchange, settings.RoutingKey);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        channel.ConfirmSelect();
        channel.BasicPublish(source.Exchange, settings.RoutingKey, true, properties, body);
        channel.WaitForConfirmsOrDie(settings.ConfirmationTimeout);
        token.ThrowIfCancellationRequested();
    }
}
