using NJsonSchema;

namespace Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

internal class MappedNameGenerator(IReadOnlyDictionary<string, Type> eventsDict) : ITypeNameGenerator
{
    private readonly Dictionary<string, string> mapping = eventsDict.DistinctBy(x => x.Value)
        .ToDictionary(x => x.Value.Name, x => x.Key);

    public string Generate(JsonSchema schema, string? typeNameHint, IEnumerable<string> reservedTypeNames) =>
        this.mapping.GetValueOrDefault(schema.Title ?? throw new("JsonSchema title is null"), schema.Title);
}
