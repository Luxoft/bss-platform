using System.Text.Json;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Internal;
using Bss.Platform.Events.Models;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Xunit;

namespace Tests.Unit.Platform.Events;

public class RabbitEventSchemaExportInitializerTests
{
    private sealed class StaticOptionsSnapshot<TOptions>(TOptions value) : IOptionsSnapshot<TOptions>
        where TOptions : class
    {
        public TOptions Value { get; } = value;

        public TOptions Get(string? name) => this.Value;
    }

    private sealed record SampleInputEvent(string Id);

    private sealed record PublicOutputEvent(string Id);

    private sealed record InternalOutputEvent(string Id);

    private sealed class FakeEventTypeProvider(
        IReadOnlyDictionary<Type, string> inputEvents,
        IReadOnlyDictionary<Type, string> outputEvents) : IEventTypeProvider
    {
        public IReadOnlyDictionary<Type, string> InputEvents { get; } = inputEvents;

        public IReadOnlyDictionary<Type, string> OutputEvents { get; } = outputEvents;
    }

    private static RabbitEventSchemaExportInitializer CreateInitializer(
        IEventTypeProvider eventTypeProvider,
        string[]? excludeOutputEvents = null,
        string exchange = "service.exchange",
        string queue = "service.queue",
        string schemaExportSystemName = "service.system")
    {
        var eventsOptions = new RabbitIntegrationEventsOptions
        {
            MessageQueue = new()
            {
                EnableSchemaExport = true,
                ExchangeName = exchange,
                QueueName = queue,
                SchemaExportSettings = new() { QueueName = "schema.export.queue", RoutingKey = "schema.export.routingKey", System = schemaExportSystemName }
            }
        };

        var bindingsOptions = new ExternalSystemBindingsOptions
        {
            ExcludeOutputEvents = excludeOutputEvents ?? [],
            SystemBindings = { ["schema.export.consumer"] = null }
        };

        var bindingsResolver = new ExternalSystemBindingsResolver(
            eventTypeProvider,
            new StaticOptionsSnapshot<ExternalSystemBindingsOptions>(bindingsOptions));

        return new RabbitEventSchemaExportInitializer(eventTypeProvider, bindingsResolver, Options.Create(eventsOptions));
    }

    [Fact]
    public void BuildExportPayloadJson_contains_required_fields_and_filtered_output_schema()
    {
        var provider = new FakeEventTypeProvider(
            new Dictionary<Type, string> { [typeof(SampleInputEvent)] = "IN.OrderAccepted" },
            new Dictionary<Type, string>
            {
                [typeof(PublicOutputEvent)] = "EXT.OrderCreated",
                [typeof(InternalOutputEvent)] = "EXT.Internal.OrderCreated"
            });

        var initializer = CreateInitializer(
            provider,
            ["EXT.Internal*"],
            "orders.exchange",
            "orders.queue",
            "orders");

        using var payload = JsonDocument.Parse(initializer.BuildExportPayloadJson("orders"));

        payload.RootElement.GetProperty("systemName").GetString().Should().Be("orders");
        payload.RootElement.GetProperty("exchange").GetString().Should().Be("orders.exchange");
        payload.RootElement.GetProperty("queue").GetString().Should().Be("orders.queue");

        var outputSchemaJson = payload.RootElement.GetProperty("output").GetRawText();
        outputSchemaJson.Should().Contain("EXT.OrderCreated");
        outputSchemaJson.Should().NotContain("EXT.Internal.OrderCreated");

        var inputSchemaJson = payload.RootElement.GetProperty("input").GetRawText();
        inputSchemaJson.Should().Contain("IN.OrderAccepted");
    }

    [Fact]
    public void BuildExportPayloadJson_keeps_provided_queue_and_system_name_values()
    {
        var provider = new FakeEventTypeProvider(
            new Dictionary<Type, string> { [typeof(SampleInputEvent)] = "IN.Event" },
            new Dictionary<Type, string> { [typeof(PublicOutputEvent)] = "EXT.Event" });

        var initializer = CreateInitializer(
            provider,
            exchange: "payments.exchange",
            queue: string.Empty,
            schemaExportSystemName: string.Empty);

        using var payload = JsonDocument.Parse(initializer.BuildExportPayloadJson(string.Empty));

        payload.RootElement.GetProperty("queue").GetString().Should().Be(string.Empty);
        payload.RootElement.GetProperty("systemName").GetString().Should().Be(string.Empty);
    }

    [Fact]
    public void ExternalSystemBindingsResolver_ResolveOutputEventsForExport_applies_exclude_patterns()
    {
        var provider = new FakeEventTypeProvider(
            new Dictionary<Type, string>(),
            new Dictionary<Type, string>
            {
                [typeof(PublicOutputEvent)] = "EXT.Public.A",
                [typeof(InternalOutputEvent)] = "EXT.Internal.B"
            });

        var options = new ExternalSystemBindingsOptions
        {
            ExcludeOutputEvents = ["EXT.Internal*"],
            SystemBindings = { ["public-queue"] = null }
        };
        var resolver = new ExternalSystemBindingsResolver(
            provider,
            new StaticOptionsSnapshot<ExternalSystemBindingsOptions>(options));

        var result = resolver.ResolveOutputEventsForExport();

        result.Keys.Should().BeEquivalentTo("EXT.Public.A");
    }
}
