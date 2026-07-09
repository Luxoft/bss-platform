using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Internal;
using Bss.Platform.Events.Models;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace Tests.Unit.Platform.Events;

public class ExternalSystemQueueBindingsInitializerTests
{
    private sealed class FakeEventTypeProvider(params string[] outputRoutingKeys) : IEventTypeProvider
    {
        // arbitrary distinct types used only as dictionary keys - the initializer only reads OutputEvents.Values
        private static readonly Type[] DummyTypes = [typeof(object), typeof(string), typeof(int), typeof(bool), typeof(double), typeof(long)];

        public IReadOnlyDictionary<Type, string> InputEvents { get; } = new Dictionary<Type, string>();

        public IReadOnlyDictionary<Type, string> OutputEvents { get; } =
            outputRoutingKeys.Select((key, i) => (key, i)).ToDictionary(x => DummyTypes[x.i], x => x.key);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string[]?> systemBindings, bool registerEventTypeProvider)
    {
        var services = new ServiceCollection();
        services.AddOptions<IntegrationEventsOptions>();
        services.AddOptions<ExternalSystemBindingsOptions>().Configure(o => o.SystemBindings = systemBindings);
        if (registerEventTypeProvider)
        {
            services.AddSingleton<IEventTypeProvider>(new FakeEventTypeProvider());
        }

        services.AddSingleton<ExternalSystemQueueBindingsInitializer>();

        return services.BuildServiceProvider();
    }

    private static ExternalSystemQueueBindingsInitializer CreateInitializer(params string[] outputRoutingKeys) =>
        new(new FakeEventTypeProvider(outputRoutingKeys), Options.Create(new IntegrationEventsOptions()), Options.Create(new ExternalSystemBindingsOptions()));

    [Fact]
    public void Resolves_when_IEventTypeProvider_is_registered()
    {
        var provider = BuildProvider(new Dictionary<string, string[]?>(), registerEventTypeProvider: true);

        var act = () => provider.GetRequiredService<ExternalSystemQueueBindingsInitializer>();

        act.Should().NotThrow();
    }

    [Fact]
    public void Throws_when_IEventTypeProvider_is_not_registered()
    {
        // the initializer requires IEventTypeProvider unconditionally, regardless of whether any bindings are configured
        var provider = BuildProvider(new Dictionary<string, string[]?>(), registerEventTypeProvider: false);

        var act = () => provider.GetRequiredService<ExternalSystemQueueBindingsInitializer>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task InitializeAsync_does_nothing_when_no_bindings_configured()
    {
        var provider = BuildProvider(new Dictionary<string, string[]?>(), registerEventTypeProvider: true);
        var initializer = provider.GetRequiredService<ExternalSystemQueueBindingsInitializer>();

        var act = () => initializer.InitializeAsync(null!, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ResolveBindings_uses_all_output_events_by_default()
    {
        var initializer = CreateInitializer("EXT.A", "EXT.B");
        var options = new ExternalSystemBindingsOptions { SystemBindings = { ["crm-queue"] = null } };

        var result = initializer.ResolveBindings(options);

        result["crm-queue"].Should().BeEquivalentTo("EXT.A", "EXT.B");
    }

    [Fact]
    public void ResolveBindings_applies_exclude_masks_only_to_default_set()
    {
        var initializer = CreateInitializer("EXT.A", "EXT.Debug.Ping", "EXT.B");
        var options = new ExternalSystemBindingsOptions
        {
            ExcludeOutputEvents = ["EXT.Debug*"],
            SystemBindings = { ["crm-queue"] = null, ["billing-queue"] = ["EXT.Debug.Ping"] }
        };

        var result = initializer.ResolveBindings(options);

        result["crm-queue"].Should().BeEquivalentTo("EXT.A", "EXT.B");
        result["billing-queue"].Should().BeEquivalentTo("EXT.Debug.Ping");
    }

    [Fact]
    public void ResolveBindings_explicit_list_overrides_default_without_validation()
    {
        var initializer = CreateInitializer("EXT.A", "EXT.B");
        var options = new ExternalSystemBindingsOptions
        {
            SystemBindings = { ["analytics-queue"] = ["EXT.NotRegisteredAnywhere"] }
        };

        var result = initializer.ResolveBindings(options);

        result["analytics-queue"].Should().BeEquivalentTo("EXT.NotRegisteredAnywhere");
    }

    [Fact]
    public void ResolveBindings_returns_empty_map_when_no_systems_configured()
    {
        var initializer = CreateInitializer("EXT.A");
        var options = new ExternalSystemBindingsOptions();

        var result = initializer.ResolveBindings(options);

        result.Should().BeEmpty();
    }
}
