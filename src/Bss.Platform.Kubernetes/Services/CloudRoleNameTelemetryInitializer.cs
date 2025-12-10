using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace Bss.Platform.Kubernetes.Services;

internal class CloudRoleNameTelemetryInitializer(IOptions<KubernetesInsightsOptions> options) : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
        {
            telemetry.Context.Cloud.RoleName = options.Value.RoleName;
        }
    }
}
