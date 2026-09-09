using System.Text.Json;

using Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

using FluentAssertions;

using Xunit;

namespace Tests.Unit.Platform.RabbitMq.JsonSchemaGeneratorBase;

public class RabbitEventsSchemaExporterTests
{
    private sealed record SampleInputEvent(string Id);

    private sealed record PublicOutputEvent(string Id);

    private sealed class TestSchemaExportSettings : IRabbitSchemaExportSettings
    {
        public required string FromQueueName { get; init; }

        public required string ExchangeName { get; init; }

        public required string System { get; init; }

        public IReadOnlyDictionary<string, Type> InputEvents { get; init; } = new Dictionary<string, Type>();

        public IReadOnlyDictionary<string, Type> OutputEvents { get; init; } = new Dictionary<string, Type>();
    }

    private static RabbitEventsSchemaExporter CreateExporter(
        IReadOnlyDictionary<string, Type> inputEvents,
        IReadOnlyDictionary<string, Type> outputEvents,
        string exchange = "service.exchange",
        string queue = "service.queue",
        string systemName = "service.system") =>
        new(
            new TestSchemaExportSettings
            {
                InputEvents = inputEvents,
                OutputEvents = outputEvents,
                ExchangeName = exchange,
                FromQueueName = queue,
                System = systemName
            });

    [Fact]
    public void BuildExportPayloadJson_contains_required_fields_and_event_schemas()
    {
        var exporter = CreateExporter(
            new Dictionary<string, Type> { ["IN.OrderAccepted"] = typeof(SampleInputEvent) },
            new Dictionary<string, Type> { ["EXT.OrderCreated"] = typeof(PublicOutputEvent) },
            "orders.exchange",
            "orders.queue",
            "orders");

        using var payload = JsonDocument.Parse(exporter.BuildExportPayloadJson());

        payload.RootElement.GetProperty("systemName").GetString().Should().Be("orders");
        payload.RootElement.GetProperty("exchange").GetString().Should().Be("orders.exchange");
        payload.RootElement.GetProperty("queue").GetString().Should().Be("orders.queue");
        payload.RootElement.GetProperty("input").GetRawText().Should().Contain("IN.OrderAccepted");
        payload.RootElement.GetProperty("output").GetRawText().Should().Contain("EXT.OrderCreated");
    }

    [Fact]
    public void BuildExportPayloadJson_keeps_provided_empty_queue_and_system_name_values()
    {
        var exporter = CreateExporter(
            new Dictionary<string, Type> { ["IN.Event"] = typeof(SampleInputEvent) },
            new Dictionary<string, Type> { ["EXT.Event"] = typeof(PublicOutputEvent) },
            "payments.exchange",
            string.Empty,
            string.Empty);

        using var payload = JsonDocument.Parse(exporter.BuildExportPayloadJson());

        payload.RootElement.GetProperty("queue").GetString().Should().Be(string.Empty);
        payload.RootElement.GetProperty("systemName").GetString().Should().Be(string.Empty);
    }
}
