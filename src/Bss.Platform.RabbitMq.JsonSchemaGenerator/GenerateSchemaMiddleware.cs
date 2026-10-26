using Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

using Microsoft.AspNetCore.Http;

namespace Bss.Platform.RabbitMq.JsonSchemaGenerator;

public class GenerateSchemaMiddleware(RequestDelegate next, string path, Dictionary<string, Type> eventsDict)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == "GET"
            && context.Request.Path.Value.Equals(path, StringComparison.InvariantCultureIgnoreCase))
        {
            var schemaContainer = new RabbitEventsSchemaGenerator().GenerateSchema(eventsDict);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(schemaContainer.ToJson());
            return;
        }

        await next(context);
    }
}
