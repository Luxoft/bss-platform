using Bss.Platform.Logging.Sinks;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;

namespace Bss.Platform.Logging;

public static class DependencyInjection
{
    public static void AddPlatformLogging(this IHostBuilder builder) =>
        builder
            .UseSerilog(
                (context, services, configuration) =>
                    configuration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .WriteTo.Sink(new ApplicationInsightsSink(services.GetService<TelemetryClient>())),
                true);
}
