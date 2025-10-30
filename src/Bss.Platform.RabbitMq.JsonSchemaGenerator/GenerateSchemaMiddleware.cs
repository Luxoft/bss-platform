using Microsoft.AspNetCore.Http;

using NJsonSchema;
using NJsonSchema.Generation;

namespace Bss.Platform.RabbitMq.JsonSchemaGenerator;

public class GenerateSchemaMiddleware(
    RequestDelegate next,
    string path,
    Func<IEnumerable<(string RoutingKey, Type EventType)>> consumedEventMappingFn,
    Func<IEnumerable<(string RoutingKey, Type EventType)>> producedEventMappingFn)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Value.Equals(path, StringComparison.InvariantCultureIgnoreCase)
            && context.Request.Method == "GET")
        {
            await this.GenerateSchema(context);
            return;
        }

        await next(context);
    }

    private async Task GenerateSchema(HttpContext context)
    {
        var settings = new SystemTextJsonSchemaGeneratorSettings
        {
            FlattenInheritanceHierarchy = true,
            GenerateAbstractProperties = false,
            AllowReferencesWithProperties = false
        };

        var typeMapping = producedEventMappingFn.Invoke()
            .Concat(consumedEventMappingFn.Invoke())
            .ToArray();

        var schemaContainer = new JsonSchema();
        var appender = new JsonSchemaAppender(schemaContainer, new MappedNameGenerator(typeMapping));
        var generator = new NJsonSchema.Generation.JsonSchemaGenerator(settings);

        var jsonSchemas = typeMapping.Select(x => x.EventType).Select(generator.Generate);
        foreach (var schema in jsonSchemas)
        {
            appender.AppendSchema(schema, null);
        }

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(schemaContainer.ToJson());
    }
}

file class MappedNameGenerator(IEnumerable<(string, Type)> sourceMapping) : ITypeNameGenerator
{
    private readonly Dictionary<string, string> mapping = sourceMapping.ToDictionary(x => x.Item2.Name, x => x.Item1);

    public string Generate(JsonSchema schema, string? typeNameHint, IEnumerable<string> reservedTypeNames) =>
        this.mapping[schema.Title ?? throw new("JsonSchema title is null")];
}
