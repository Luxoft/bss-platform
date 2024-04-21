using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal record SynchronizedConsumer(
    SqlConnectionStringProvider ConnectionStringProvider,
    IRabbitMqConsumerLockService LockService,
    ILogger<SynchronizedConsumer> Logger,
    IRabbitMqMessageReader MessageReader,
    IOptions<RabbitMqConsumerSettings> ConsumerSettings)
    : IRabbitMqConsumer
{
    private SqlConnection? connection;

    private DateTime? lockObtainedDate;

    public async Task ConsumeAsync(IModel channel, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (await this.GetLock(token))
                {
                    await this.MessageReader.ReadAsync(channel, token);
                }
                else
                {
                    await this.CloseConnectionAsync();
                    await Delay(this.ConsumerSettings.Value.InactiveConsumerSleepMilliseconds, token);
                }
            }
            catch (Exception e)
            {
                this.Logger.LogError(e, "Consuming error");
                await this.CloseConnectionAsync();
                await Delay(this.ConsumerSettings.Value.InactiveConsumerSleepMilliseconds, token);
            }
        }
    }

    public void Dispose()
    {
        if (this.connection is not null)
        {
            this.LockService.TryReleaseLock(this.connection);
        }

        this.connection?.Close();
        GC.SuppressFinalize(this);
    }

    private async Task<bool> GetLock(CancellationToken token)
    {
        if (this.lockObtainedDate?.AddMilliseconds(this.ConsumerSettings.Value.ActiveConsumerRefreshMilliseconds) >= DateTime.Now)
        {
            return true;
        }

        await this.OpenConnectionAsync(token);
        if (!this.LockService.TryObtainLock(this.connection!))
        {
            return false;
        }

        this.lockObtainedDate = DateTime.Now;
        this.Logger.LogDebug("Current consumer is active");

        return true;
    }

    private async Task OpenConnectionAsync(CancellationToken token)
    {
        await this.CloseConnectionAsync();

        this.connection = new SqlConnection(this.ConnectionStringProvider.ConnectionString);
        await this.connection.OpenAsync(token);
    }

    private async Task CloseConnectionAsync()
    {
        try
        {
            if (this.connection is not null)
            {
                this.LockService.TryReleaseLock(this.connection);
                await this.connection!.CloseAsync();
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to close connection");
        }
    }

    private static Task Delay(int value, CancellationToken token) => Task.Delay(TimeSpan.FromMilliseconds(value), token);
}
