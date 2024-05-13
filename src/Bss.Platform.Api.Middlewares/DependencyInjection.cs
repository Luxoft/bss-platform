using Microsoft.AspNetCore.Builder;

namespace Bss.Platform.Api.Middlewares;

public static class DependencyInjection
{
    public static IApplicationBuilder UsePlatformErrorsMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<ErrorsMiddleware>();
}
