using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Services;

/// <summary>Runs registered initializers before consumers start and shares successful initialization.</summary>
public class RabbitMqInitializationService(
    IRabbitMqClient client,
    IEnumerable<IRabbitInitializer> initializers,
    IOptions<RabbitMqSettings> options,
    IHostApplicationLifetime? applicationLifetime = null) : IHostedService, IDisposable
{
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = applicationLifetime is null
        ? new CancellationTokenSource()
        : CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime.ApplicationStopping);
    private Task? initialization;
    private Task? startup;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            this.lifetime.Token.ThrowIfCancellationRequested();
            this.startup ??= this.GetOrStartInitialization();
            return this.startup.WaitAsync(cancellationToken);
        }
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            return this.GetOrStartInitialization().WaitAsync(cancellationToken);
        }
    }

    private Task GetOrStartInitialization()
    {
        var token = this.lifetime.Token;
        token.ThrowIfCancellationRequested();
        if (this.initialization is null || this.initialization.IsFaulted || this.initialization.IsCanceled)
        {
            this.initialization = Task.Run(() => this.InitializeAsync(token), token);
            // A caller can abandon its wait before the shared attempt fails.
            _ = this.initialization.ContinueWith(static task => { _ = task.Exception; },
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return this.initialization;
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

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await this.lifetime.CancelAsync();
        Task? pending;
        lock (this.sync)
        {
            pending = this.initialization;
        }

        if (pending is not null)
        {
            await Task.WhenAny(pending).WaitAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        this.lifetime.Cancel();
        this.lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
