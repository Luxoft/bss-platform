using System.Text;

using Bss.Platform.Events;
using Bss.Platform.Events.Models;

using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace Tests.Unit.Platform.Events;

public class DependencyInjectionSystemBindingsTests
{
    // matches the example from IntegrationEventsMessageQueueOptions.EnableExternalSystemBindings XML doc
    private const string Json = """
        {
          "RabbitCap": {
            "ExternalSystemBindings": {
              "ExcludeOutputEvents": ["EXT.Debug*", "EXT.Internal.SomeEvent"],
              "SystemBindings": {
                "system1-allEvents-queue-except-excluded": null,
                "system2-allEvents-queue-except-excluded": [],
                "system3-fixedEvents-queue-exclude-not-applied": ["EXT.OrderCreated", "EXT.OrderCancelled", "EXT.Debug.Some"]
              }
            }
          }
        }
        """;

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(Json)))
            .Build();

    [Fact]
    public void ResolveSystemBindings_preserves_all_keys_including_null_and_empty_array_values()
    {
        var configuration = BuildConfiguration();

        var systemBindings = DependencyInjection.ResolveSystemBindings(configuration, "RabbitCap:ExternalSystemBindings");

        // null and [] both mean "no explicit routing keys" (see ExternalSystemBindingsOptions.SystemBindings doc)
        // and the JSON provider's raw representation of that distinction is an undocumented implementation
        // detail that differs across framework versions - only assert both keys survive, not which shape they take.
        systemBindings.Should().HaveCount(3);
        systemBindings.Should().ContainKey("system1-allEvents-queue-except-excluded")
            .WhoseValue.Should().BeNullOrEmpty();
        systemBindings.Should().ContainKey("system2-allEvents-queue-except-excluded")
            .WhoseValue.Should().BeNullOrEmpty();
        systemBindings["system3-fixedEvents-queue-exclude-not-applied"]
            .Should().BeEquivalentTo("EXT.OrderCreated", "EXT.OrderCancelled", "EXT.Debug.Some");
    }

    // reproduces AddExternalSystemQueueBindings's own registration (BindConfiguration + Configure<IConfiguration>)
    // to guard the contract: a consumer's PostConfigure must always win, regardless of call order.
    private static IServiceCollection RegisterLikeLibraryDoes(IConfiguration configuration) =>
        new ServiceCollection()
            .AddSingleton(configuration)
            .AddOptions<ExternalSystemBindingsOptions>()
            .BindConfiguration("RabbitCap:ExternalSystemBindings")
            .Configure<IConfiguration>((options, config) =>
                options.SystemBindings = DependencyInjection.ResolveSystemBindings(config, "RabbitCap:ExternalSystemBindings"))
            .Services;

    [Fact]
    public void Consumer_PostConfigure_registered_after_library_setup_overrides_SystemBindings()
    {
        var services = RegisterLikeLibraryDoes(BuildConfiguration());
        services.PostConfigure<ExternalSystemBindingsOptions>(o => o.SystemBindings = new() { ["overridden-queue"] = ["EXT.Custom"] });

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ExternalSystemBindingsOptions>>().Value;

        options.SystemBindings.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new KeyValuePair<string, string[]?>("overridden-queue", ["EXT.Custom"]));
    }

    [Fact]
    public void Consumer_PostConfigure_registered_before_library_setup_still_overrides_SystemBindings()
    {
        IServiceCollection services = new ServiceCollection();
        services.PostConfigure<ExternalSystemBindingsOptions>(o => o.SystemBindings = new() { ["overridden-queue"] = ["EXT.Custom"] });

        foreach (var descriptor in RegisterLikeLibraryDoes(BuildConfiguration()))
        {
            services.Add(descriptor);
        }

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ExternalSystemBindingsOptions>>().Value;

        options.SystemBindings.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new KeyValuePair<string, string[]?>("overridden-queue", ["EXT.Custom"]));
    }
}
