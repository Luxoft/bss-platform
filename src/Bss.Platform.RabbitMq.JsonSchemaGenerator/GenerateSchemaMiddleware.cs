using Microsoft.AspNetCore.Http;

using NJsonSchema;
using NJsonSchema.Generation;

namespace Bss.Platform.RabbitMq.JsonSchemaGenerator;

public class GenerateSchemaMiddleware(RequestDelegate next, string path, Dictionary<string, Type> eventsDict)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == "GET"
            && context.Request.Path.Value.Equals(path, StringComparison.InvariantCultureIgnoreCase))
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
        
        var schemaContainer = new JsonSchema();
        var appender = new JsonSchemaAppender(schemaContainer, new MappedNameGenerator(eventsDict));
        var generator = new NJsonSchema.Generation.JsonSchemaGenerator(settings);

        var jsonSchemas = eventsDict.Select(x => x.Value).Select(generator.Generate);
        foreach (var schema in jsonSchemas)
        {
            appender.AppendSchema(schema, null);
        }

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(schemaContainer.ToJson());
    }
}

file class MappedNameGenerator(Dictionary<string, Type> eventsDict) : ITypeNameGenerator
{
    private readonly Dictionary<string, string> mapping = eventsDict.DistinctBy(x => x.Value)
        .ToDictionary(x => x.Value.Name, x => x.Key);

    public string Generate(JsonSchema schema, string? typeNameHint, IEnumerable<string> reservedTypeNames) =>
        this.mapping.GetValueOrDefault(schema.Title ?? throw new("JsonSchema title is null"), schema.Title);
}
