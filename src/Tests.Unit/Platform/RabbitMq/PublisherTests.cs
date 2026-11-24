using System.Text.Json;

using Bss.Platform.RabbitMq;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Services;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Xunit;

namespace Tests.Unit.Platform.RabbitMq;

public class PublisherTests
{
    public record RegisteredEvent(int Id);
    public record DerivedEvent(int Id, string Details) : RegisteredEvent(Id);
    public record UnknownEvent(string Name);

    [Fact]
    public async Task Registered_event_uses_catalog_route_and_persistent_confirmed_json()
    {
        using var fixture = new Fixture();
        await fixture.Publisher.PublishAsync(new RegisteredEvent(42));
        var publish = Assert.Single(fixture.Channels.Single().Calls, x => x.Method == nameof(IModel.BasicPublish));
        Assert.Equal("events", publish.Arguments[0]);
        Assert.Equal("Orders.Created", publish.Arguments[1]);
        Assert.Equal(true, publish.Arguments[2]);
        using var json = JsonDocument.Parse((ReadOnlyMemory<byte>)publish.Arguments[4]!);
        Assert.Equal(42, json.RootElement.GetProperty("Id").GetInt32());
        Assert.Contains(fixture.Properties.Single().Calls, x => x.Method == "set_ContentType" && Equals(x.Arguments[0], "application/json"));
        Assert.Contains(fixture.Properties.Single().Calls, x => x.Method == "set_Persistent" && Equals(x.Arguments[0], true));
        Assert.Contains(fixture.Channels.Single().Calls, x => x.Method == nameof(IModel.ConfirmSelect));
        Assert.Contains(fixture.Channels.Single().Calls, x => x.Method == nameof(IModel.WaitForConfirmsOrDie));
        Assert.Contains(fixture.Channels.Single().Calls, x => x.Method == nameof(IDisposable.Dispose));
    }

    [Fact]
    public async Task Unknown_type_throws_before_initialization_or_connection()
    {
        using var fixture = new Fixture();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Publisher.PublishAsync(new UnknownEvent("unknown")));
        Assert.Contains(typeof(UnknownEvent).FullName!, error.Message);
        Assert.Contains("not registered", error.Message);
        Assert.Equal(0, fixture.Client.Connections);
        Assert.Empty(fixture.Channels);
    }

    [Fact]
    public async Task Runtime_type_is_used_and_base_registration_does_not_authorize_subclasses()
    {
        using var unregistered = new Fixture();
        RegisteredEvent message = new DerivedEvent(12, "details");
        await Assert.ThrowsAsync<InvalidOperationException>(() => unregistered.Publisher.PublishAsync(message));
        Assert.Equal(0, unregistered.Client.Connections);
        using var fixture = new Fixture(configureCatalog: catalog => catalog.AddOutputEvent<DerivedEvent>("Orders.Detailed"));
        await fixture.Publisher.PublishAsync(message);
        var publication = Assert.Single(fixture.Channels.Single().Calls, x => x.Method == nameof(IModel.BasicPublish));
        Assert.Equal("Orders.Detailed", publication.Arguments[1]);
        using var json = JsonDocument.Parse((ReadOnlyMemory<byte>)publication.Arguments[4]!);
        Assert.Equal("details", json.RootElement.GetProperty("Details").GetString());
    }

    [Fact]
    public async Task Virtual_fallback_can_generate_a_route_for_unknown_types()
    {
        using var fixture = new Fixture(fallback: true);
        await fixture.Publisher.PublishAsync(new UnknownEvent("fallback"));
        await fixture.Publisher.PublishAsync(new RegisteredEvent(1));
        Assert.Equal(nameof(UnknownEvent), Assert.Single(fixture.Channels[0].Calls, x => x.Method == nameof(IModel.BasicPublish)).Arguments[1]);
        Assert.Equal("Orders.Created", Assert.Single(fixture.Channels[1].Calls, x => x.Method == nameof(IModel.BasicPublish)).Arguments[1]);
    }

    [Fact]
    public async Task Ambiguous_registered_type_throws_without_publishing_to_multiple_routes()
    {
        using var fixture = new Fixture(configureCatalog: catalog => catalog.AddOutputEvent<RegisteredEvent>("Orders.Alias"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Publisher.PublishAsync(new RegisteredEvent(1)));
        Assert.Contains("multiple routing keys", error.Message);
        Assert.Equal(0, fixture.Client.Connections);
    }

    [Fact]
    public async Task Routing_index_reads_catalog_once_for_known_and_unknown_types()
    {
        var catalog = new CountingCatalog();
        using var fixture = new Fixture(catalog: catalog);
        Assert.Equal(1, catalog.Reads);
        await fixture.Publisher.PublishAsync(new RegisteredEvent(1));
        await fixture.Publisher.PublishAsync(new RegisteredEvent(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Publisher.PublishAsync(new UnknownEvent("unknown")));
        Assert.Equal(1, catalog.Reads);
    }

    [Fact]
    public async Task Publications_reuse_connection_and_use_separate_channels_until_disposal()
    {
        var fixture = new Fixture();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 5).Select(x => fixture.Publisher.PublishAsync(new RegisteredEvent(x))));
            Assert.Equal(1, fixture.Client.Connections);
            Assert.Equal(5, fixture.Channels.Count);
            Assert.DoesNotContain(fixture.Connection.Calls, x => x.Method == nameof(IDisposable.Dispose));
            fixture.Publisher.Dispose();
            Assert.Single(fixture.Connection.Calls, x => x.Method == nameof(IDisposable.Dispose));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Publisher.PublishAsync(new RegisteredEvent(1)));
        }
        finally { fixture.Dispose(); }
    }

    [Fact]
    public async Task Returned_message_is_reported_as_failure_even_when_confirmed()
    {
        using var fixture = new Fixture { ReturnMessage = true };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Publisher.PublishAsync(new RegisteredEvent(1)));
        Assert.Contains("NO_ROUTE", error.Message);
        Assert.Contains(fixture.Channels.Single().Calls, x => x.Method == "remove_BasicReturn");
    }

    [Fact]
    public async Task Concurrent_first_calls_share_a_single_pending_connection()
    {
        var (connection, proxy) = RecordingProxy<IConnection>.Create();
        var channelCount = 0;
        proxy.Handler = (method, _) =>
        {
            if (method.Name == "get_IsOpen") return true;
            if (method.Name != nameof(IConnection.CreateModel)) return null;
            Interlocked.Increment(ref channelCount);
            var (channel, channelProxy) = RecordingProxy<IModel>.Create();
            var (properties, _) = RecordingProxy<IBasicProperties>.Create();
            channelProxy.Handler = (operation, _) => operation.Name == nameof(IModel.CreateBasicProperties) ? properties : null;
            return channel;
        };
        var client = new DelayedClient();
        var common = Options.Create(new RabbitMqSettings { Exchange = "events" });
        using var publisher = new RabbitMqEventPublisher(new OutputEventCatalog().AddOutputEvent<RegisteredEvent>("Event"), client,
            new RabbitMqInitializationService(client, [], common), common, Options.Create(new RabbitMqProducerSettings()));
        var publications = Enumerable.Range(0, 5).Select(x => publisher.PublishAsync(new RegisteredEvent(x))).ToArray();
        await client.Connecting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, client.Connections);
        Assert.All(publications, task => Assert.False(task.IsCompleted));
        client.Ready.SetResult(connection);
        await Task.WhenAll(publications);
        Assert.Equal(1, client.Connections);
        Assert.Equal(5, channelCount);
    }

    [Fact]
    public async Task Confirmation_failure_is_propagated_and_not_retried()
    {
        using var fixture = new Fixture { FailConfirmation = true };
        await Assert.ThrowsAsync<IOException>(() => fixture.Publisher.PublishAsync(new RegisteredEvent(1)));
        Assert.Single(fixture.Channels.Single().Calls, x => x.Method == nameof(IModel.BasicPublish));
        Assert.Contains(fixture.Channels.Single().Calls, x => x.Method == nameof(IDisposable.Dispose));
    }

    [Fact]
    public async Task Null_and_cancelled_calls_do_not_connect()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Publisher.PublishAsync(null!));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Publisher.PublishAsync(new RegisteredEvent(1), cancellation.Token));
        Assert.Equal(0, fixture.Client.Connections);
    }

    [Fact]
    public async Task Cancellation_during_a_successful_confirmation_preserves_success()
    {
        using var cancellation = new CancellationTokenSource();
        using var fixture = new Fixture { OnConfirmation = cancellation.Cancel };

        await fixture.Publisher.PublishAsync(new RegisteredEvent(1), cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Single(fixture.Channels.Single().Calls, x => x.Method == nameof(IModel.BasicPublish));
    }

    [Fact]
    public void Publisher_is_registered_by_default_and_can_be_replaced_before_platform_registration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformRabbitMqClient(new ConfigurationBuilder().Build());
        using (var provider = services.BuildServiceProvider())
            Assert.IsType<RabbitMqEventPublisher>(provider.GetRequiredService<IRabbitMqEventPublisher>());
        var custom = new NoOpPublisher();
        var customServices = new ServiceCollection();
        customServices.AddSingleton<IRabbitMqEventPublisher>(custom);
        customServices.AddPlatformRabbitMqClient(new ConfigurationBuilder().Build());
        using var overridden = customServices.BuildServiceProvider();
        Assert.Same(custom, overridden.GetRequiredService<IRabbitMqEventPublisher>());
    }

    private sealed class Fixture : IDisposable
    {
        public OutputEventCatalog Catalog { get; } = new OutputEventCatalog().AddOutputEvent<RegisteredEvent>("Orders.Created");
        public InitializationTests.FakeClient Client { get; }
        public RabbitMqEventPublisher Publisher { get; }
        public RecordingProxy<IConnection> Connection { get; }
        public List<RecordingProxy<IModel>> Channels { get; } = [];
        public List<RecordingProxy<IBasicProperties>> Properties { get; } = [];
        public bool ReturnMessage { get; init; }
        public bool FailConfirmation { get; init; }
        public Action? OnConfirmation { get; init; }

        public Fixture(bool fallback = false, Action<OutputEventCatalog>? configureCatalog = null, IOutputEventCatalog? catalog = null)
        {
            configureCatalog?.Invoke(this.Catalog);
            var (connection, proxy) = RecordingProxy<IConnection>.Create();
            this.Connection = proxy;
            proxy.Handler = (method, _) => method.Name switch
            {
                "get_IsOpen" => true,
                nameof(IConnection.CreateModel) => this.CreateChannel(),
                _ => null
            };
            this.Client = new InitializationTests.FakeClient(connection);
            var options = Options.Create(new RabbitMqSettings { Exchange = "events" });
            var producer = Options.Create(new RabbitMqProducerSettings());
            var initialization = new RabbitMqInitializationService(this.Client, [], options);
            this.Publisher = fallback
                ? new NameFallbackPublisher(catalog ?? this.Catalog, this.Client, initialization, options, producer)
                : new RabbitMqEventPublisher(catalog ?? this.Catalog, this.Client, initialization, options, producer);
        }

        private IModel CreateChannel()
        {
            var (channel, proxy) = RecordingProxy<IModel>.Create();
            var (properties, propertiesProxy) = RecordingProxy<IBasicProperties>.Create();
            this.Channels.Add(proxy);
            this.Properties.Add(propertiesProxy);
            EventHandler<BasicReturnEventArgs>? returned = null;
            proxy.Handler = (method, args) =>
            {
                switch (method.Name)
                {
                    case nameof(IModel.CreateBasicProperties): return properties;
                    case "add_BasicReturn": returned += (EventHandler<BasicReturnEventArgs>)args[0]!; break;
                    case "remove_BasicReturn": returned -= (EventHandler<BasicReturnEventArgs>)args[0]!; break;
                    case nameof(IModel.BasicPublish) when this.ReturnMessage:
                        returned?.Invoke(channel, new BasicReturnEventArgs { ReplyCode = 312, ReplyText = "NO_ROUTE" });
                        break;
                    case nameof(IModel.WaitForConfirmsOrDie) when this.FailConfirmation: throw new IOException("confirmation failed");
                    case nameof(IModel.WaitForConfirmsOrDie): this.OnConfirmation?.Invoke(); break;
                }
                return null;
            };
            return channel;
        }

        public void Dispose() => this.Publisher.Dispose();
    }

    private sealed class NameFallbackPublisher(
        IOutputEventCatalog catalog, IRabbitMqClient client, RabbitMqInitializationService initialization,
        IOptions<RabbitMqSettings> options, IOptions<RabbitMqProducerSettings> producer)
        : RabbitMqEventPublisher(catalog, client, initialization, options, producer)
    {
        protected override string ResolveUnregisteredRoutingKey(Type eventType) => eventType.Name;
    }

    private sealed class NoOpPublisher : IRabbitMqEventPublisher
    {
        public Task PublishAsync(object @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CountingCatalog : IOutputEventCatalog
    {
        private readonly IReadOnlyDictionary<string, Type> events = new Dictionary<string, Type> { ["Orders.Created"] = typeof(RegisteredEvent) };
        public int Reads { get; private set; }
        public IReadOnlyDictionary<string, Type> Events
        {
            get
            {
                this.Reads++;
                return this.events;
            }
        }
    }

    private sealed class DelayedClient : IRabbitMqClient
    {
        public TaskCompletionSource Connecting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IConnection?> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Connections { get; private set; }
        public Task<IConnection?> TryConnectAsync(int? attempts, CancellationToken token = default)
        {
            this.Connections++;
            this.Connecting.TrySetResult();
            return this.Ready.Task.WaitAsync(token);
        }
    }
}
