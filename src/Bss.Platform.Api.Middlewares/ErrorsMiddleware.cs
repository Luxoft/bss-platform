using System.Net;
using System.Net.Mime;

using Bss.Platform.Api.Middlewares.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bss.Platform.Api.Middlewares;

public class ErrorsMiddleware(RequestDelegate next, ILogger<ErrorsMiddleware> logger, IServiceProvider serviceProvider)
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
        context.Response.ContentType = MediaTypeNames.Text.Plain;
        context.Response.StatusCode =
            (int)(serviceProvider.GetService<IStatusCodeResolver>()?.Resolve(exception) ?? HttpStatusCode.InternalServerError);

        return context.Response.WriteAsync(exception.GetBaseException().Message);
    }
}
