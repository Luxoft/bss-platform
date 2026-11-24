using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Services;

/// <summary>Runs registered initializers once, before consumers start; owns only startup connections.</summary>
public class RabbitMqInitializationService(
    IRabbitMqClient client,
    IEnumerable<IRabbitInitializer> initializers,
    IOptions<RabbitMqSettings> options) : IHostedService
{
    private readonly object sync = new();
    private Task? initialization;

    public Task StartAsync(CancellationToken cancellationToken) => this.EnsureInitializedAsync(cancellationToken);

    public Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        lock (this.sync)
        {
            return this.initialization ??= Task.Run(() => this.InitializeAsync(cancellationToken), cancellationToken);
        }
    }

    public async Task<bool> WaitForStartupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await this.StartAsync(cancellationToken);
            return true;
        }
        catch (RabbitMqStartupConnectionException)
        {
            return false;
        }
    }

    internal void ValidateConfiguration()
    {
        _ = options.Value;
        foreach (var initializer in initializers)
        {
            _ = initializer.IsEnabled;
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var enabled = initializers.Where(x => x.IsEnabled).OrderBy(x => x.Order).ToArray();
        if (enabled.Length == 0)
        {
            return;
        }

        IConnection? connection = null;
        try
        {
            foreach (var initializer in enabled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await initializer.ExecuteAsync(CreateChannelAsync, cancellationToken);
            }
        }
        finally
        {
            connection?.Dispose();
        }

        async Task<IModel> CreateChannelAsync(CancellationToken token)
        {
            connection ??= await client.TryConnectAsync(options.Value.ConnectionAttemptCount, token)
                           ?? throw new RabbitMqStartupConnectionException();
            return connection.CreateModel();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
