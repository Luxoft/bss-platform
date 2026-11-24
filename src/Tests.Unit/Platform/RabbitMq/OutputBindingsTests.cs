using Bss.Platform.RabbitMq;
using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Services;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using Xunit;

namespace Tests.Unit.Platform.RabbitMq;

public class OutputBindingsTests
{
    [Fact]
    public void Default_and_explicit_routes_preserve_source_semantics()
    {
        var catalog = new OutputEventCatalog().AddOutputEvent<string>("Orders.Created").AddOutputEvent<int>("Orders.Internal.Debug");
        var settings = new RabbitMqProducerSettings
        {
            DefaultExcludeOutputBindings = ["orders.internal*"],
            SystemBindings = new()
            {
                ["ToBilling"] = null, ["ToAnalytics"] = [],
                ["ToCrm"] = ["Orders.Internal.Debug", "Unregistered", "Unregistered"]
            }
        };
        var resolver = new OutputBindingsResolver(catalog, Options.Create(settings));
        Assert.Equal(new[] { "Orders.Created" }, resolver.ResolveQueueBindings()["ToBilling"]);
        Assert.Equal(new[] { "Orders.Created" }, resolver.ResolveQueueBindings()["ToAnalytics"]);
        Assert.Equal(new[] { "Orders.Internal.Debug", "Unregistered" }, resolver.ResolveQueueBindings()["ToCrm"]);
        Assert.Equal(2, resolver.ResolveSchemaEvents().Count);
    }

    [Fact]
    public void Schema_selection_distinguishes_absent_empty_and_partial_systems()
    {
        var catalog = new OutputEventCatalog().AddOutputEvent<string>("A").AddOutputEvent<int>("B");
        var settings = new RabbitMqProducerSettings();
        var resolver = new OutputBindingsResolver(catalog, Options.Create(settings));
        Assert.Equal(2, resolver.ResolveSchemaEvents().Count);
        settings.SystemBindings = [];
        Assert.Empty(resolver.ResolveSchemaEvents());
        settings.SystemBindings["ToBilling"] = ["B", "Unknown"];
        Assert.Equal(typeof(int), Assert.Single(resolver.ResolveSchemaEvents()).Value);
    }

    [Fact]
    public void Configuration_preserves_null_empty_and_explicit_systems_and_postconfigure_wins()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Producer:SystemBindings:ToBilling"] = null,
            ["RabbitMQ:Producer:SystemBindings:ToAnalytics"] = "",
            ["RabbitMQ:Producer:SystemBindings:ToCrm:0"] = "A",
            ["RabbitMQ:Producer:DefaultExcludeOutputBindings:0"] = "Internal*"
        }).Build();
        var services = new ServiceCollection();
        services.AddPlatformRabbitMqClient(configuration).RegisterRabbitMqOutputEvents(x => x.AddOutputEvent<string>("A"));
        services.PostConfigure<RabbitMqProducerSettings>(x => x.SystemBindings!["ToExtra"] = ["B"]);
        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<RabbitMqProducerSettings>>().Value;
        Assert.Equal(4, settings.SystemBindings!.Count);
        Assert.Equal(new[] { "Internal*" }, settings.DefaultExcludeOutputBindings);
        var bindings = provider.GetRequiredService<IOutputBindingsResolver>().ResolveQueueBindings();
        Assert.Equal(new[] { "A" }, bindings["ToBilling"]);
        Assert.Equal(new[] { "A" }, bindings["ToAnalytics"]);
        Assert.Equal(new[] { "B" }, bindings["ToExtra"]);
    }

    [Fact]
    public void Explicit_empty_configuration_is_not_absent()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Producer:SystemBindings"] = null
        }).Build();
        var services = new ServiceCollection();
        services.AddPlatformRabbitMqClient(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Empty(provider.GetRequiredService<IOptions<RabbitMqProducerSettings>>().Value.SystemBindings!);
    }

    [Fact]
    public async Task Output_initializer_declares_exchange_before_binding_and_uses_queue_policy()
    {
        var catalog = new OutputEventCatalog().AddOutputEvent<string>("A");
        var settings = new RabbitMqProducerSettings { SystemBindings = new() { ["ToBilling"] = null } };
        var resolver = new OutputBindingsResolver(catalog, Options.Create(settings));
        var initializer = new OutputQueuesInitializer(resolver, Options.Create(new RabbitMqSettings { Exchange = "events" }), new QuorumQueueDeclarationPolicy());
        var (channel, proxy) = RecordingProxy<IModel>.Create();
        await initializer.InitializeAsync(channel, default);
        Assert.Equal(nameof(IModel.ExchangeDeclare), proxy.Calls[0].Method);
        Assert.Equal(nameof(IModel.QueueDeclare), proxy.Calls[1].Method);
        Assert.Equal("quorum", ((IDictionary<string, object>)proxy.Calls[1].Arguments[4]!)["x-queue-type"]);
        Assert.Equal(nameof(IModel.QueueBind), proxy.Calls[2].Method);
        Assert.Equal("ToBilling", proxy.Calls[2].Arguments[0]);
        Assert.Equal("A", proxy.Calls[2].Arguments[2]);
    }
}
