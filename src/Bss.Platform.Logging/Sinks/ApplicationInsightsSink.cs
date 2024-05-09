using Microsoft.ApplicationInsights;

using Serilog.Core;
using Serilog.Events;

namespace Bss.Platform.Logging.Sinks;

public class ApplicationInsightsSink(TelemetryClient? telemetryClient) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        if (telemetryClient is null || logEvent.Exception == null)
        {
            return;
        }

        telemetryClient.TrackException(logEvent.Exception, logEvent.Properties.ToDictionary(x => x.Key, x => x.Value.ToString()));
    }
}
