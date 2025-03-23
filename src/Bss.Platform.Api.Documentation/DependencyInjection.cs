using Bss.Platform.Api.Documentation.SchemaFilters;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bss.Platform.Api.Documentation;

public static class DependencyInjection
{
    private const string AuthorizationScheme = "Bearer";

    public static IServiceCollection AddPlatformApiDocumentation(
        this IServiceCollection services,
        IWebHostEnvironment hostEnvironment,
        string title = "API",
        Action<SwaggerGenOptions>? setupAction = null,
        bool enableExperimentalRequiredFeature = false)
    {
        if (hostEnvironment.IsProduction())
        {
            return services;
        }

        return services
               .AddEndpointsApiExplorer()
               .AddSwaggerGen(
                   x =>
                   {
                       x.SwaggerDoc("api", new OpenApiInfo { Title = title });
                       x.SchemaFilter<XEnumNamesSchemaFilter>();
                       
                       if (enableExperimentalRequiredFeature)
                       {
                           x.SupportNonNullableReferenceTypes();
                           x.NonNullableReferenceTypesAsRequired();
                           x.SchemaFilter<ExcludeNullableFromRequiredSchemaFilter>();
                       }

                       x.AddSecurityDefinition(
                           AuthorizationScheme,
                           new OpenApiSecurityScheme
                           {
                               Name = "Authorization",
                               Description = "Specify token",
                               In = ParameterLocation.Header,
                               Type = SecuritySchemeType.ApiKey,
                               Scheme = AuthorizationScheme
                           });

                       x.AddSecurityRequirement(
                           new OpenApiSecurityRequirement
                           {
                               {
                                   new OpenApiSecurityScheme
                                   {
                                       Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = AuthorizationScheme }
                                   },
                                   new List<string>()
                               }
                           });

                       setupAction?.Invoke(x);
                   });
    }

    public static IApplicationBuilder UsePlatformApiDocumentation(
        this IApplicationBuilder app,
        IWebHostEnvironment hostEnvironment,
        string path = "swagger")
    {
        if (hostEnvironment.IsProduction())
        {
            return app;
        }

        return app
               .UseSwagger(x => { x.RouteTemplate = $"{path}/{{documentName}}/swagger.json"; })
               .UseSwaggerUI(
                   x =>
                   {
                       x.RoutePrefix = path;
                       x.SwaggerEndpoint($"/{path}/api/swagger.json", "api");
                   });
    }
}
