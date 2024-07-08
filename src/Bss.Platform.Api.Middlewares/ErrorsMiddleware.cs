using System.Net;
using System.Net.Mime;

using Bss.Platform.Api.Middlewares.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bss.Platform.Api.Middlewares;

public class ErrorsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<ErrorsMiddleware> logger, IServiceProvider serviceProvider)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Request failed");

            await HandleExceptionAsync(context, e, serviceProvider.GetService<IStatusCodeResolver>());
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IStatusCodeResolver? statusCodeResolver)
    {
        context.Response.ContentType = MediaTypeNames.Text.Plain;
        context.Response.StatusCode = (int)(statusCodeResolver?.Resolve(exception) ?? HttpStatusCode.InternalServerError);

        await context.Response.WriteAsync(exception.GetBaseException().Message, context.RequestAborted);
    }
}
