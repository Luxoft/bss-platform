using System.Text.Json;

using Bss.Platform.RabbitMq.Base;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Schemas;

public class RabbitSchemaSender(
    IOptions<RabbitSchemaOptions> options,
    IQueueDeclarationPolicy queuePolicy,
    RabbitSchemaMessageBuilder messageBuilder) : IRabbitSchemaSender
{
    protected virtual string DestinationQueue => options.Value.QueueName;
    protected virtual TimeSpan ConfirmationTimeout => TimeSpan.FromSeconds(5);

    public virtual Task SendAsync(IModel channel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken cancellationToken = default) =>
        this.SendIfEnabledAsync(() =>
        {
            this.Publish(channel, types, source, cancellationToken);
            return Task.CompletedTask;
        }, source, cancellationToken);

    public virtual Task SendAsync(Func<CancellationToken, Task<IModel>> createChannel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken cancellationToken = default) =>
        this.SendIfEnabledAsync(async () =>
        {
            using var channel = await createChannel(cancellationToken);
            this.Publish(channel, types, source, cancellationToken);
        }, source, cancellationToken);

    private async Task SendIfEnabledAsync(Func<Task> send, RabbitSchemaSource source, CancellationToken token)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Exchange);
        token.ThrowIfCancellationRequested();
        await send();
    }

    private void Publish(IModel channel, IRabbitSchemaTypeProvider types, RabbitSchemaSource source, CancellationToken token)
    {
        var settings = options.Value;
        var timeout = this.ConfirmationTimeout;
        if (timeout <= TimeSpan.Zero)
            throw new InvalidOperationException("ConfirmationTimeout must be positive.");
        var body = JsonSerializer.SerializeToUtf8Bytes(messageBuilder.Build(types, source, settings.SystemName));
        token.ThrowIfCancellationRequested();
        channel.ExchangeDeclare(source.Exchange, ExchangeType.Topic, true);
        queuePolicy.Declare(channel, this.DestinationQueue);
        channel.QueueBind(this.DestinationQueue, source.Exchange, settings.RoutingKey);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        channel.ConfirmSelect();
        token.ThrowIfCancellationRequested();
        channel.BasicPublish(source.Exchange, settings.RoutingKey, true, properties, body);
        channel.WaitForConfirmsOrDie(timeout);
    }
}
