using Bss.Platform.Events.Interfaces;

using DotNetCore.CAP.RabbitMQ;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bss.Platform.Events.Internal;

internal sealed partial class RabbitInitializersHostedService(
    IConnectionChannelPool connectionChannelPool,
    IEnumerable<IRabbitInitializer> initializers,
    ILogger<RabbitInitializersHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var initializer in initializers)
        {
            try
            {
                using var channel = connectionChannelPool.Rent();
                await initializer.InitializeAsync(channel, cancellationToken);
            }
            catch (Exception ex)
            {
                this.LogInitializerFailed(initializer.GetType().Name, ex);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(LogLevel.Error, Message = "Rabbit initializer {InitializerName} failed to run")]
    partial void LogInitializerFailed(string initializerName, Exception ex);
}
