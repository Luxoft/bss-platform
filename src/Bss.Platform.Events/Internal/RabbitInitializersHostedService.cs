using Bss.Platform.Events.Interfaces;

using DotNetCore.CAP.RabbitMQ;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace Bss.Platform.Events.Internal;

internal sealed partial class RabbitInitializersHostedService(
    IConnectionChannelPool connectionChannelPool,
    IEnumerable<IRabbitInitializer> initializers,
    ILogger<RabbitInitializersHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var channel = connectionChannelPool.Rent();
        await this.RunInitializersAsync(channel, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal async Task RunInitializersAsync(IModel channel, CancellationToken cancellationToken)
    {
        foreach (var initializer in initializers)
        {
            try
            {
                await initializer.InitializeAsync(channel, cancellationToken);
            }
            catch (Exception ex)
            {
                this.LogInitializerFailed(initializer.GetType().Name, ex);
            }
        }
    }

    [LoggerMessage(LogLevel.Error, Message = "Rabbit initializer {InitializerName} failed to run")]
    partial void LogInitializerFailed(string initializerName, Exception ex);
}
