using Bss.Platform.RabbitMq;
using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Consumer;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Services;
using Bss.Platform.RabbitMq.Consumer.Settings;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Services;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using Xunit;

namespace Tests.Unit.Platform.RabbitMq;

public class InitializationTests
{
    public sealed class FakeClient(IConnection? connection) : IRabbitMqClient
    {
        public IConnection? Connection { get; set; } = connection;
        public int Connections { get; private set; }
        public Task<IConnection?> TryConnectAsync(int? attempts, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            this.Connections++;
            return Task.FromResult(this.Connection);
        }
    }

    private sealed class WaitingInitializer(TaskCompletionSource entered, TaskCompletionSource finish) : IRabbitInitializer
    {
        public Task InitializeAsync(IModel channel, CancellationToken cancellationToken)
        {
            entered.SetResult();
            return finish.Task.WaitAsync(cancellationToken);
        }
    }

    public class Processor : IRabbitMqMessageProcessor
    {
        public Task ProcessAsync(IBasicProperties properties, string routingKey, string message, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class Initializer(int order, List<int> calls, bool fail = false) : IRabbitInitializer
    {
        public int Order => order;
        public Task InitializeAsync(IModel channel, CancellationToken cancellationToken)
        {
            calls.Add(order);
            if (fail) throw new InvalidOperationException("topology failed");
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("errors")]
    public void Consumer_declares_optional_deadletter_queue_using_shared_policy(string? deadLetterQueue)
    {
        var (model, proxy) = RecordingProxy<IModel>.Create();
        var settings = new RabbitMqConsumerSettings { Queue = "in", DeadLetterQueue = deadLetterQueue, RoutingKeys = ["Event"] };
        var initializer = new ConsumerInitializer(Options.Create(settings), Options.Create(new RabbitMqSettings { Exchange = "events" }), new QuorumQueueDeclarationPolicy());
        initializer.Initialize(model);
        var queues = proxy.Calls.Where(x => x.Method == nameof(IModel.QueueDeclare)).ToArray();
        Assert.Equal(string.IsNullOrEmpty(deadLetterQueue) ? 1 : 2, queues.Length);
        Assert.All(queues, call => Assert.Equal("quorum", ((IDictionary<string, object>)call.Arguments[4]!)["x-queue-type"]));
        Assert.Contains(proxy.Calls, call => call.Method == nameof(IModel.QueueBind) && Equals(call.Arguments[0], "in") && Equals(call.Arguments[2], "Event"));
        if (!string.IsNullOrEmpty(deadLetterQueue))
            Assert.Contains(proxy.Calls, call => call.Method == nameof(IModel.QueueBind) && Equals(call.Arguments[0], deadLetterQueue) && Equals(call.Arguments[1], "deadletters"));
    }

    [Fact]
    public async Task Connection_only_client_does_no_startup_work()
    {
        var client = new FakeClient(null);
        var services = new ServiceCollection();
        services.AddSingleton<IRabbitMqClient>(client);
        services.AddPlatformRabbitMqClient(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<RabbitMqInitializationService>().StartAsync(default);
        Assert.Equal(0, client.Connections);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Host_applies_startup_policy_when_broker_is_unavailable(bool throwOnFailure)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Startup:ThrowOnFailure"] = throwOnFailure.ToString(),
            ["RabbitMQ:ConnectionAttemptCount"] = "0"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        var client = new FakeClient(null);
        services.AddSingleton<IRabbitMqClient>(client);
        services.AddSingleton<IRabbitInitializer>(new Initializer(0, []));
        services.AddPlatformRabbitMqClient(configuration);
        using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<IHostedService>();

        if (throwOnFailure)
        {
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => host.StartAsync(default));
        }
        else
        {
            await host.StartAsync(default);
            await ((BackgroundService)host).ExecuteTask!;
            Assert.False(await provider.GetRequiredService<RabbitMqInitializationService>().WaitForStartupAsync(default));
        }

        Assert.Equal(1, client.Connections);
    }

    [Fact]
    public async Task Initialization_is_ordered_once_and_disposes_startup_resources()
    {
        var (model, modelProxy) = RecordingProxy<IModel>.Create();
        var (connection, connectionProxy) = RecordingProxy<IConnection>.Create();
        connectionProxy.Handler = (method, _) => method.Name == nameof(IConnection.CreateModel) ? model : null;
        var client = new FakeClient(connection);
        var calls = new List<int>();
        var service = new RabbitMqInitializationService(client, [new Initializer(20, calls), new Initializer(0, calls)], Options.Create(new RabbitMqSettings()));
        await Task.WhenAll(service.StartAsync(default), service.EnsureInitializedAsync(default));
        Assert.Equal(new[] { 0, 20 }, calls);
        Assert.Equal(1, client.Connections);
        Assert.Equal(2, modelProxy.Calls.Count(x => x.Method == nameof(IDisposable.Dispose)));
        Assert.Contains(connectionProxy.Calls, x => x.Method == nameof(IDisposable.Dispose));
    }

    [Fact]
    public async Task Required_topology_failure_stops_later_initializers()
    {
        var (model, _) = RecordingProxy<IModel>.Create();
        var (connection, proxy) = RecordingProxy<IConnection>.Create();
        proxy.Handler = (method, _) => method.Name == nameof(IConnection.CreateModel) ? model : null;
        var calls = new List<int>();
        var service = new RabbitMqInitializationService(new FakeClient(connection), [new Initializer(0, calls, true), new Initializer(1, calls)], Options.Create(new RabbitMqSettings()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(default));
        Assert.Equal(new[] { 0 }, calls);
    }

    [Fact]
    public async Task Failed_startup_can_be_retried_explicitly_without_restarting_the_consumer()
    {
        var client = new FakeClient(null);
        var calls = new List<int>();
        using var service = new RabbitMqInitializationService(client, [new Initializer(0, calls)], Options.Create(new RabbitMqSettings()));
        Assert.False(await service.WaitForStartupAsync(default));

        var (model, _) = RecordingProxy<IModel>.Create();
        var (connection, proxy) = RecordingProxy<IConnection>.Create();
        proxy.Handler = (method, _) => method.Name == nameof(IConnection.CreateModel) ? model : null;
        client.Connection = connection;
        await service.EnsureInitializedAsync(default);
        await service.EnsureInitializedAsync(default);

        Assert.False(await service.WaitForStartupAsync(default));
        Assert.Single(calls);
        Assert.Equal(2, client.Connections);
    }

    [Fact]
    public async Task Cancelling_one_wait_leaves_the_shared_attempt_running()
    {
        var (model, _) = RecordingProxy<IModel>.Create();
        var (connection, proxy) = RecordingProxy<IConnection>.Create();
        proxy.Handler = (method, _) => method.Name == nameof(IConnection.CreateModel) ? model : null;
        var client = new FakeClient(connection);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = new RabbitMqInitializationService(client, [new WaitingInitializer(entered, finish)], Options.Create(new RabbitMqSettings()));
        using var cancellation = new CancellationTokenSource();
        var first = service.EnsureInitializedAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = service.EnsureInitializedAsync(default);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(second.IsCompleted);
        finish.SetResult();
        await second;
        Assert.Equal(1, client.Connections);
    }

    [Fact]
    public async Task Application_stopping_cancels_initialization_before_hosted_services_stop()
    {
        using var host = Host.CreateApplicationBuilder().Build();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var (model, _) = RecordingProxy<IModel>.Create();
        var (connection, proxy) = RecordingProxy<IConnection>.Create();
        proxy.Handler = (method, _) => method.Name == nameof(IConnection.CreateModel) ? model : null;
        var client = new FakeClient(connection);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = new RabbitMqInitializationService(client, [new WaitingInitializer(entered, finish)],
            Options.Create(new RabbitMqSettings()), lifetime);
        var attempt = service.EnsureInitializedAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        lifetime.StopApplication();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attempt.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = service.EnsureInitializedAsync(default); });
        Assert.Equal(1, client.Connections);
    }

    [Fact]
    public void Legacy_exchange_and_programmatic_consumer_settings_are_supported()
    {
        var services = new ServiceCollection();
        services.AddPlatformRabbitMqClient(new ConfigurationBuilder().Build());
        services.AddPlatformRabbitMqConsumer<Processor>(new ConfigurationBuilder().Build());
        services.Configure<RabbitMqConsumerSettings>(x => x.Exchange = "legacy");
        using var provider = services.BuildServiceProvider();
        Assert.Equal("legacy", provider.GetRequiredService<IOptions<RabbitMqSettings>>().Value.Exchange);
        services.RemoveRabbitMqInitializer<ConsumerTopologyInitializer>();
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IRabbitInitializer) && x.ImplementationType == typeof(ConsumerTopologyInitializer));
    }

    [Fact]
    public void Conflicting_exchanges_fail_validation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Exchange"] = "new", ["RabbitMQ:Consumer:Exchange"] = "old"
        }).Build();
        var services = new ServiceCollection();
        services.AddPlatformRabbitMqClient(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<RabbitMqSettings>>().Value);
    }

    [Fact]
    public void Postconfigure_legacy_consumer_overrides_are_used_for_common_exchange()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Consumer:Exchange"] = "configured"
        }).Build();
        var services = new ServiceCollection();
        services.AddPlatformRabbitMqClient(configuration).AddPlatformRabbitMqConsumer<Processor>(configuration);
        services.PostConfigure<RabbitMqConsumerSettings>(x => x.Exchange = "overridden");
        using var provider = services.BuildServiceProvider();
        Assert.Equal("overridden", provider.GetRequiredService<IOptions<RabbitMqSettings>>().Value.Exchange);
    }

    [Theory]
    [InlineData(null, 4, 4)]
    [InlineData(null, null, null)]
    [InlineData("0", 4, 0)]
    public void Connection_limit_prefers_common_setting_then_effective_consumer_options(string? common, int? legacy, int? expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:ConnectionAttemptCount"] = common,
            ["RabbitMQ:Consumer:ConnectionAttemptCount"] = "9"
        }).Build();
        var services = new ServiceCollection();
        services.AddPlatformRabbitMqClient(configuration).AddPlatformRabbitMqConsumer<Processor>(configuration);
        services.PostConfigure<RabbitMqConsumerSettings>(x => x.ConnectionAttemptCount = legacy);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(expected, provider.GetRequiredService<IOptions<RabbitMqSettings>>().Value.ConnectionAttemptCount);
    }
}
