using System.Collections.Frozen;
using System.Text.Json;

using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bss.Platform.RabbitMq.Services;

/// <summary>Publishes declared output contracts using their exact runtime types.</summary>
public class RabbitMqEventPublisher(
    IOutputEventCatalog catalog,
    IRabbitMqClient client,
    RabbitMqInitializationService initialization,
    IOptions<RabbitMqSettings> options,
    IOptions<RabbitMqProducerSettings> producerOptions) : IRabbitMqEventPublisher, IDisposable
{
    private readonly FrozenDictionary<Type, string[]> routesByType = catalog.Events
        .GroupBy(x => x.Value)
        .ToFrozenDictionary(x => x.Key, x => x.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).ToArray());

    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private IConnection? connection;
    private int disposed;

    public async Task PublishAsync(object @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        // Resolve/serialize before opening connections or performing startup work.
        var eventType = @event.GetType();
        var routingKey = this.ResolveRoutingKey(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        var body = this.Serialize(@event);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Exchange))
            throw new InvalidOperationException("RabbitMQ:Exchange is required for event publication.");
        var timeout = producerOptions.Value.PublishConfirmationTimeout;
        if (timeout <= TimeSpan.Zero)
            throw new InvalidOperationException("RabbitMQ:Producer:PublishConfirmationTimeout must be positive.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.lifetime.Token);
        var token = linked.Token;
        await initialization.EnsureInitializedAsync(token);
        var activeConnection = await this.GetConnectionAsync(settings.ConnectionAttemptCount, token);
        using var channel = activeConnection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.ContentType = "application/json";
        properties.Persistent = true;

        BasicReturnEventArgs? returned = null;
        void OnReturned(object? sender, BasicReturnEventArgs args) => Volatile.Write(ref returned, args);
        channel.BasicReturn += OnReturned;
        try
        {
            channel.ConfirmSelect();
            token.ThrowIfCancellationRequested();
            channel.BasicPublish(settings.Exchange, routingKey, true, properties, body);
            channel.WaitForConfirmsOrDie(timeout);
            var rejection = Volatile.Read(ref returned);
            if (rejection is not null)
                throw new InvalidOperationException($"RabbitMQ returned event '{eventType.FullName}' on route '{routingKey}': {rejection.ReplyCode} {rejection.ReplyText}.");
        }
        finally
        {
            channel.BasicReturn -= OnReturned;
        }
    }

    protected virtual string ResolveRoutingKey(Type eventType)
    {
        if (!this.routesByType.TryGetValue(eventType, out var routes))
            return this.ResolveUnregisteredRoutingKey(eventType);

        return routes.Length switch
        {
            1 => routes[0],
            _ => throw new InvalidOperationException($"Output event type '{eventType.FullName}' has multiple routing keys: {string.Join(", ", routes)}. Override ResolveRoutingKey to select one explicitly.")
        };
    }

    /// <summary>Override to support unregistered types. The default fails before any broker operations.</summary>
    protected virtual string ResolveUnregisteredRoutingKey(Type eventType) =>
        throw new InvalidOperationException($"Output event type '{eventType.FullName}' is not registered. Register it with RegisterRabbitMqOutputEvents or override ResolveUnregisteredRoutingKey.");

    protected virtual byte[] Serialize(object @event) => JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());

    private async Task<IConnection> GetConnectionAsync(int? attempts, CancellationToken token)
    {
        await this.connectionLock.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
            if (this.connection is null || !this.connection.IsOpen)
            {
                this.connection?.Dispose();
                this.connection = await client.TryConnectAsync(attempts, token)
                                  ?? throw new InvalidOperationException("RabbitMQ event publisher could not establish a connection.");
            }
            return this.connection;
        }
        finally
        {
            this.connectionLock.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
        this.lifetime.Cancel();
        this.connectionLock.Wait();
        try { this.connection?.Dispose(); }
        finally { this.connectionLock.Release(); }
        // Keep the gate/token source available to in-flight callers observing shutdown.
        GC.SuppressFinalize(this);
    }
}
