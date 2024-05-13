using System.Net;
using System.Net.Mime;
using System.Text.Json;

using Bss.Platform.Api.Middlewares.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bss.Platform.Api.Middlewares;

public class ErrorsMiddleware(RequestDelegate next, ILogger<ErrorsMiddleware> logger, IStatusCodeResolver? statusCodeResolver)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Request failed");

            await this.HandleExceptionAsync(context, e);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;
        context.Response.StatusCode = (int)(statusCodeResolver?.Resolve(exception) ?? HttpStatusCode.InternalServerError);

        return context.Response.WriteAsync(JsonSerializer.Serialize(exception.GetBaseException().Message));
    }
}
