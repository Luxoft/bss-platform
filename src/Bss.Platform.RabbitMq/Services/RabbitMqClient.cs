using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Services;

public class RabbitMqClient(IOptions<RabbitMqServerSettings> options, ILogger<RabbitMqClient> logger) : IRabbitMqClient
{
    private const int RetryConnectDelay = 5000;

    public Task<IConnection?> TryConnectAsync(int? attempts, CancellationToken token = default)
    {
        var serverSettings = options.Value;
        var factory = new ConnectionFactory
                      {
                          HostName = serverSettings.Host,
                          Port = serverSettings.Port,
                          UserName = serverSettings.UserName,
                          Password = serverSettings.Secret,
                          VirtualHost = serverSettings.VirtualHost,
                          AutomaticRecoveryEnabled = true
                      };

        var policy = this.CreateRetryPolicy(attempts);

        try
        {
            return policy.ExecuteAsync(_ => Task.FromResult(factory.CreateConnection()), token)!;
        }
        catch (Exception ex)
        {
            this.LogConnectionError(ex);
            return Task.FromResult<IConnection?>(null);
        }
    }

    private AsyncRetryPolicy CreateRetryPolicy(int? attempts)
    {
        var builder = Policy.Handle<Exception>();
        if (attempts is null)
        {
            return builder.WaitAndRetryForeverAsync(
                _ => TimeSpan.FromMilliseconds(RetryConnectDelay),
                (ex, _) => this.LogConnectionError(ex));
        }

        return builder
            .WaitAndRetryAsync(
                attempts.Value,
                _ => TimeSpan.FromMilliseconds(RetryConnectDelay),
                (ex, _) => this.LogConnectionError(ex));
    }

    private void LogConnectionError(Exception exception) => logger.LogError(exception, "Could not connect to RabbitMQ server");
}
