using Bss.Platform.Kubernetes.Services;

using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        if (options.SkipDefaultHealthChecks || options.AdditionalHealthCheckPathToSkip.Length > 0)
        {
            services.AddSingleton<ITelemetryInitializer, HealthCheckSkipInitializer>();
        }

        if (options.SkipSuccessfulDependency)
        {
            services.AddApplicationInsightsTelemetryProcessor<SuccessfulDependencyFilter>();
        }

        if (options.LogFilterRules is { Count: > 0 } rules)
        {
            services.AddLogMessagesToAppInsight(rules);
        }

        services.AddSingleton<ITelemetryInitializer, TelemetryDataEnrichInitializer>();

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

    private static IServiceCollection AddLogMessagesToAppInsight(this IServiceCollection services, List<KubernetesInsightsOptions.LogFilterRule> rules) =>
        services.AddLogging(x =>
        {
            x.AddApplicationInsights();
            const string appInsightProvider = "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider";
            var loggerFilterRulesWithIndex = rules
                .Select(r => new LoggerFilterRule(
                            appInsightProvider,
                            r.Category,
                            r.Level,
                            r.Filter == null ? null : (_, c, l) => r.Filter(c, l)))
                .Select((value, index) => (index, value));

            x.Services.Configure<LoggerFilterOptions>(o =>
            {
                foreach (var r in loggerFilterRulesWithIndex)
                {
                    // NOTE: insert with index need to have ability override settings from config (another sources):
                    // https://github.com/microsoft/ApplicationInsights-dotnet/blob/98bc6c540e2dcccc78e4b356cd70e03d146a01ad/NETCORE/src/Shared/Extensions/ApplicationInsightsExtensions.cs#L398
                    o.Rules.Insert(r.index, r.value);
                }
            });
        });
}
