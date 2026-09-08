using System.Text;
using System.Text.Json;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

public sealed class RabbitEventsSchemaExporter(IRabbitSchemaExportSettings settings)
{
    public void Export(IModel model, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exportExchange = settings.ExchangeName;
        var exportQueue = settings.ToQueueName;

        model.QueueDeclare(exportQueue, true, false, false, null);
        model.QueueBind(exportQueue, exportExchange, settings.RoutingKey);

        var properties = model.CreateBasicProperties();
        properties.DeliveryMode = 2;
        properties.ContentType = "application/json";

        var payloadJson = this.BuildExportPayloadJson();
        model.BasicPublish(exportExchange, settings.RoutingKey, properties, Encoding.UTF8.GetBytes(payloadJson));
        if (model.NextPublishSeqNo > 0)
        {
            model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
        }
    }

    internal string BuildExportPayloadJson()
    {
        var inputSchema = GenerateSchemaJson(settings.InputEvents);
        var outputSchema = GenerateSchemaJson(settings.OutputEvents);

        using var inputDocument = JsonDocument.Parse(inputSchema);
        using var outputDocument = JsonDocument.Parse(outputSchema);

        var payload = new
        {
            output = outputDocument.RootElement.Clone(),
            input = inputDocument.RootElement.Clone(),
            systemName = settings.System,
            exchange = settings.ExchangeName,
            queue = settings.FromQueueName
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string GenerateSchemaJson(IReadOnlyDictionary<string, Type> eventsDict)
    {
        var schemaGenerator = new RabbitEventsSchemaGenerator();
        var schemaContainer = schemaGenerator.GenerateSchema(eventsDict);

        return schemaContainer.ToJson();
    }
}
