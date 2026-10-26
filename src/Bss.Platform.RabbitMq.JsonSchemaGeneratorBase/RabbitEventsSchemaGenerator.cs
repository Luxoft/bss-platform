using NJsonSchema;
using NJsonSchema.Generation;

namespace Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

public class RabbitEventsSchemaGenerator
{
    public JsonSchema GenerateSchema(IReadOnlyDictionary<string, Type> eventsDict)
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

        var jsonSchemas = eventsDict.Select(x => x.Value).Select(generator.Generate);
        foreach (var schema in jsonSchemas)
        {
            appender.AppendSchema(schema, null);
        }

        return schemaContainer;
    }
}
