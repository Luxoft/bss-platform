using Microsoft.AspNetCore.Http;

using Bss.Platform.RabbitMq.Schemas;

namespace Bss.Platform.RabbitMq.JsonSchemaGenerator;

public class GenerateSchemaMiddleware(RequestDelegate next, string path, Dictionary<string, Type> eventsDict)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == "GET"
            && string.Equals(context.Request.Path.Value, path, StringComparison.InvariantCultureIgnoreCase))
        {
            await this.GenerateSchema(context);
            return;
        }

        await next(context);
    }

    private async Task GenerateSchema(HttpContext context)
    {
        var schemaContainer = new RabbitEventsSchemaGenerator().GenerateSchema(eventsDict);

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(schemaContainer.ToJson());
    }
}
