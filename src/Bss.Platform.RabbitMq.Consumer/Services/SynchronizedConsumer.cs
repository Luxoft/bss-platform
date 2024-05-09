using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal class SynchronizedConsumer(
    SqlConnectionStringProvider connectionStringProvider,
    IRabbitMqConsumerLockService lockService,
    ILogger<SynchronizedConsumer> logger,
    IRabbitMqMessageReader messageReader,
    IOptions<RabbitMqConsumerSettings> consumerSettings)
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
                    await messageReader.ReadAsync(channel, token);
                }
                else
                {
                    await this.CloseConnectionAsync();
                    await Delay(consumerSettings.Value.InactiveConsumerSleepMilliseconds, token);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Consuming error");
                await this.CloseConnectionAsync();
                await Delay(consumerSettings.Value.InactiveConsumerSleepMilliseconds, token);
            }
        }
    }

    public void Dispose()
    {
        if (this.connection is not null)
        {
            lockService.TryReleaseLock(this.connection);
        }

        this.connection?.Close();
        GC.SuppressFinalize(this);
    }

    private async Task<bool> GetLock(CancellationToken token)
    {
        if (this.lockObtainedDate?.AddMilliseconds(consumerSettings.Value.ActiveConsumerRefreshMilliseconds) >= DateTime.Now)
        {
            return true;
        }

        await this.OpenConnectionAsync(token);
        if (!lockService.TryObtainLock(this.connection!))
        {
            return false;
        }

        this.lockObtainedDate = DateTime.Now;
        logger.LogDebug("Current consumer is active");

        return true;
    }

    private async Task OpenConnectionAsync(CancellationToken token)
    {
        await this.CloseConnectionAsync();

        this.connection = new SqlConnection(connectionStringProvider.ConnectionString);
        await this.connection.OpenAsync(token);
    }

    private async Task CloseConnectionAsync()
    {
        try
        {
            if (this.connection is not null)
            {
                lockService.TryReleaseLock(this.connection);
                await this.connection!.CloseAsync();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to close connection");
        }
    }

    private static Task Delay(int value, CancellationToken token) => Task.Delay(TimeSpan.FromMilliseconds(value), token);
}
