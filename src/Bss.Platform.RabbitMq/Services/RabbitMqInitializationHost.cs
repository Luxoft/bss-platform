using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bss.Platform.RabbitMq.Services;

internal sealed class RabbitMqInitializationHost(
    RabbitMqInitializationService initialization,
    IOptions<RabbitMqStartupSettings> startup,
    IOptions<RabbitMqServerSettings> server,
    ILogger<RabbitMqInitializationHost> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = server.Value;
        initialization.ValidateConfiguration();
        var throwOnFailure = startup.Value.ThrowOnFailure;
        try
        {
            await initialization.StartAsync(cancellationToken);
        }
        catch (RabbitMqStartupConnectionException exception) when (!throwOnFailure)
        {
            logger.LogError(exception, "RabbitMQ topology initialization could not connect to the broker");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await initialization.StopAsync(cancellationToken);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => initialization.StopAsync(cancellationToken);
}
