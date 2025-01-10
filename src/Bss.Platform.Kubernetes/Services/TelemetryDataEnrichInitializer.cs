using System.Reflection;

using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bss.Platform.Kubernetes.Services;

internal class TelemetryDataEnrichInitializer(IHttpContextAccessor accessor, IOptions<KubernetesInsightsOptions> options) : TelemetryInitializerBase(accessor)
{
    protected override void OnInitializeTelemetry(HttpContext platformContext, RequestTelemetry requestTelemetry, ITelemetry telemetry)
    {
        if (options.Value.SetAuthenticatedUserFromHttpContext
            && string.IsNullOrEmpty(telemetry.Context.User.AuthenticatedUserId)
            && platformContext.User.Identity?.Name is { Length: > 0 } username)
        {
            telemetry.Context.User.AuthenticatedUserId = username;
        }

        if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
        {
            telemetry.Context.Cloud.RoleName = options.Value.RoleName;
        }
    }
}
