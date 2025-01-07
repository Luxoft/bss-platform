using Bss.Platform.Kubernetes.Services;

using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.Kubernetes;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformKubernetesInsights(this IServiceCollection services, IConfiguration configuration, Action<KubernetesInsightsOptions>? setup = null)
    {
        var options = new KubernetesInsightsOptions();
        if (setup != null)
        {
            services.Configure(setup);
            setup.Invoke(options);
        }

        if (options.SetAuthenticatedUserFromHttpContext)
        {
            services.AddHttpContextAccessor();
        }

        if (options.SkipSuccessfulDependency)
        {
            services.AddApplicationInsightsTelemetryProcessor<SuccessfulDependencyFilter>();
        }

        services.AddSingleton<ITelemetryInitializer, BssPlatformTelemetryInitializer>();

        return services
            .AddApplicationInsightsTelemetry(configuration)
            .ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((x, _) => { x.EnableSqlCommandTextInstrumentation = true; })
            .AddApplicationInsightsKubernetesEnricher();
    }

    public static IServiceCollection AddPlatformKubernetesHealthChecks(this IServiceCollection services, string connectionString)
    {
        services
            .AddHealthChecks()
            .AddSqlServer(connectionString, name: Constants.SqlHealthCheck);

        return services;
    }

    public static IApplicationBuilder UsePlatformKubernetesHealthChecks(this IApplicationBuilder app) =>
        app
            .UseHealthChecks(Constants.LiveHealthCheck, new HealthCheckOptions { Predicate = _ => false })
            .UseHealthChecks(Constants.ReadyHealthCheck, new HealthCheckOptions { Predicate = x => x.Name == Constants.SqlHealthCheck });

    public static IHealthChecksBuilder AddPlatformKubernetesHealthChecks(
        this IServiceCollection services,
        string connectionString,
        string sqlCheckName) =>
        services
            .AddHealthChecks()
            .AddSqlServer(connectionString, name: sqlCheckName);

    public static IApplicationBuilder UsePlatformKubernetesHealthChecks(this IApplicationBuilder app, params string[] readyCheckNames) =>
        app
            .UseHealthChecks(Constants.LiveHealthCheck, new HealthCheckOptions { Predicate = _ => false })
            .UseHealthChecks(Constants.ReadyHealthCheck, new HealthCheckOptions { Predicate = x => readyCheckNames.Contains(x.Name) });
}
