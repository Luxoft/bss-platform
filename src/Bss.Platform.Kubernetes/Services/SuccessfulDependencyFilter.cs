using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Bss.Platform.Kubernetes.Services;

internal class SuccessfulDependencyFilter(ITelemetryProcessor next) : ITelemetryProcessor
{
    public void Process(ITelemetry item)
    {
        if (item is DependencyTelemetry { Success: true })
        {
            return;
        }

        next.Process(item);
    }
}
