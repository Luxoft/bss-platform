using System.Net;
using System.Net.Mime;

using Bss.Platform.Api.Middlewares.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bss.Platform.Api.Middlewares;

public class ErrorsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<ErrorsMiddleware> logger, IStatusCodeResolver? statusCodeResolver)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Request failed");

            await HandleExceptionAsync(context, e, statusCodeResolver);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IStatusCodeResolver? statusCodeResolver)
    {
        context.Response.ContentType = MediaTypeNames.Text.Plain;
        context.Response.StatusCode = (int)(statusCodeResolver?.Resolve(exception) ?? HttpStatusCode.InternalServerError);

        await context.Response.WriteAsync(exception.GetBaseException().Message, context.RequestAborted);
    }
}
