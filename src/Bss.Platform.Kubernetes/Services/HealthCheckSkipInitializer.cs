using System.Reflection;

using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bss.Platform.Kubernetes.Services;

internal class HealthCheckSkipInitializer(IHttpContextAccessor accessor, IOptions<KubernetesInsightsOptions> options) : TelemetryInitializerBase(accessor)
{
    private static string[] DefaultHealthCheckRoutes { get; } = [Constants.LiveHealthCheck, Constants.ReadyHealthCheck];

    protected override void OnInitializeTelemetry(HttpContext platformContext, RequestTelemetry requestTelemetry, ITelemetry telemetry)
    {
        var currentRequestPath = platformContext.Request.Path.Value;
        var healthCheckPaths = options.Value.AdditionalHealthCheckPathToSkip
            .Concat(options.Value.SkipDefaultHealthChecks ? DefaultHealthCheckRoutes : []);

        var isHealthCheckRequest = healthCheckPaths.Contains(currentRequestPath, StringComparer.OrdinalIgnoreCase);
        if (!isHealthCheckRequest)
        {
            return;
        }

        MarkTelemetryToSkip(telemetry);
    }

    private static void MarkTelemetryToSkip(ITelemetry telemetry)
    {
        if (telemetry is ISupportAdvancedSampling advancedSampling)
        {
            advancedSampling.ProactiveSamplingDecision = SamplingDecision.SampledOut;
        }

        if (string.IsNullOrWhiteSpace(telemetry.Context.Operation.SyntheticSource))
        {
            telemetry.Context.Operation.SyntheticSource = "HealthCheck";
        }
    }
}
