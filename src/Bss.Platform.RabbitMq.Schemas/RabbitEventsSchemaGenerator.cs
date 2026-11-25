using System.Reflection;

using Namotion.Reflection;

using NJsonSchema;
using NJsonSchema.Generation;

namespace Bss.Platform.RabbitMq.Schemas;

public class RabbitEventsSchemaGenerator
{
    public virtual JsonSchema GenerateSchema(IReadOnlyDictionary<string, Type> events)
    {
        var container = new JsonSchema();
        foreach (var (route, type) in events.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(route);
            var settings = new SystemTextJsonSchemaGeneratorSettings
            {
                FlattenInheritanceHierarchy = true,
                GenerateAbstractProperties = false,
                AllowReferencesWithProperties = false
            };
            // Separate roots preserve aliases. Use an uncached contextual root: Namotion's
            // default cache keys by FullName and can mix contracts from different assemblies.
            var schema = new JsonSchema();
            new JsonSchemaGenerator(settings).Generate(schema, type.ToContextualType(type.GetCustomAttributes()), new JsonSchemaResolver(schema, settings));
            container.Definitions.Add(route, schema);
        }
        return container;
    }
}
