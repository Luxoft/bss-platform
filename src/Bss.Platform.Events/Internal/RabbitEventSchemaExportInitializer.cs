using System.Text;
using System.Text.Json;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;
using Bss.Platform.Events.Models;

using Microsoft.Extensions.Options;

using NJsonSchema;
using NJsonSchema.Generation;

using RabbitMQ.Client;

namespace Bss.Platform.Events.Internal;

internal sealed class RabbitEventSchemaExportInitializer(
    IEventTypeProvider eventTypeProvider,
    IExternalSystemBindingsResolver bindingsResolver,
    IOptions<RabbitIntegrationEventsOptions> eventOptions) : IRabbitInitializer
{
    private readonly IntegrationEventsMessageQueueOptions messageQueueOptions = eventOptions.Value.MessageQueue;

    public Task InitializeAsync(IModel model, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = this.messageQueueOptions.SchemaExportSettings;

        if (settings == null)
        {
            return Task.CompletedTask;
        }

        var exportExchange = this.messageQueueOptions.ExchangeName;
        var exportQueue = settings.QueueName;

        model.QueueDeclare(exportQueue, true, false, false, null);
        model.QueueBind(exportQueue, exportExchange, settings.RoutingKey);

        var properties = model.CreateBasicProperties();
        properties.DeliveryMode = 2;
        properties.ContentType = "application/json";

        var payloadJson = this.BuildExportPayloadJson(settings.System);
        model.BasicPublish(exportExchange, settings.RoutingKey, properties, Encoding.UTF8.GetBytes(payloadJson));
        if (model.NextPublishSeqNo > 0)
        {
            model.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
        }

        return Task.CompletedTask;
    }

    internal string BuildExportPayloadJson(string systemName)
    {
        var inputSchema = GenerateSchemaJson(eventTypeProvider.InputEvents.ToDictionary(x => x.Value, x => x.Key));
        var outputSchema = GenerateSchemaJson(bindingsResolver.ResolveOutputEventsForExport());

        using var inputDocument = JsonDocument.Parse(inputSchema);
        using var outputDocument = JsonDocument.Parse(outputSchema);

        var payload = new
        {
            output = outputDocument.RootElement.Clone(),
            input = inputDocument.RootElement.Clone(),
            systemName = systemName,
            exchange = this.messageQueueOptions.ExchangeName,
            queue = this.messageQueueOptions.QueueName
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string GenerateSchemaJson(IReadOnlyDictionary<string, Type> eventsDict)
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings
        {
            FlattenInheritanceHierarchy = true,
            GenerateAbstractProperties = false,
            AllowReferencesWithProperties = false
        };

        var schemaContainer = new JsonSchema();
        var appender = new JsonSchemaAppender(schemaContainer, new MappedNameGenerator(eventsDict));
        var generator = new JsonSchemaGenerator(settings);

        var jsonSchemas = eventsDict.Select(x => x.Value).Distinct().Select(generator.Generate);
        foreach (var schema in jsonSchemas)
        {
            appender.AppendSchema(schema, null);
        }

        return schemaContainer.ToJson();
    }

    private sealed class MappedNameGenerator(IReadOnlyDictionary<string, Type> eventsDict) : ITypeNameGenerator
    {
        private readonly Dictionary<string, string> mapping = eventsDict.DistinctBy(x => x.Value)
            .ToDictionary(x => x.Value.Name, x => x.Key);

        public string Generate(JsonSchema schema, string? typeNameHint, IEnumerable<string> reservedTypeNames) =>
            this.mapping.GetValueOrDefault(schema.Title ?? throw new("JsonSchema title is null"), schema.Title);
    }
}
