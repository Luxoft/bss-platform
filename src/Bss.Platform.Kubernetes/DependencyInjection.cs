using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.Kubernetes;

public static class DependencyInjection
{
    private const string SqlHealthCheck = "SQLCheck";

    public static IServiceCollection AddPlatformKubernetesInsights(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddApplicationInsightsTelemetry(configuration)
            .ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((x, _) => { x.EnableSqlCommandTextInstrumentation = true; })
            .AddApplicationInsightsKubernetesEnricher();

    public static IServiceCollection AddPlatformKubernetesHealthChecks(this IServiceCollection services, string connectionString)
    {
        services
            .AddHealthChecks()
            .AddSqlServer(connectionString, name: SqlHealthCheck);

        return services;
    }

    public static IApplicationBuilder UsePlatformKubernetesHealthChecks(this IApplicationBuilder app) =>
        app
            .UseHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .UseHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Name == SqlHealthCheck });
}
