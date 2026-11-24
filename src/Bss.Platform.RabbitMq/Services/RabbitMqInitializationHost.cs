using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bss.Platform.RabbitMq.Services;

internal sealed class RabbitMqInitializationHost(
    RabbitMqInitializationService initialization,
    IOptions<RabbitMqStartupSettings> startup,
    IOptions<RabbitMqServerSettings> server,
    ILogger<RabbitMqInitializationHost> logger) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _ = server.Value;
        initialization.ValidateConfiguration();
        return startup.Value.ThrowOnFailure
            ? this.StartRequiredAsync(cancellationToken)
            : base.StartAsync(cancellationToken);
    }

    private async Task StartRequiredAsync(CancellationToken cancellationToken)
    {
        try
        {
            await initialization.StartAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await initialization.StopAsync(cancellationToken);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            await initialization.StartAsync(stoppingToken);
        }
        catch (RabbitMqStartupConnectionException exception)
        {
            logger.LogError(exception, "RabbitMQ topology initialization could not connect to the broker");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await initialization.StopAsync(cancellationToken);
    }
}
