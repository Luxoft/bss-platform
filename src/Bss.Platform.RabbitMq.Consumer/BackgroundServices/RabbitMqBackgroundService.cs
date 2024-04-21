using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;
using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.BackgroundServices;

internal class RabbitMqBackgroundService(
    IRabbitMqClient client,
    IEnumerable<IRabbitMqInitializer> initializers,
    IRabbitMqConsumer consumer,
    IOptions<RabbitMqServerSettings> serverSettings,
    IOptions<RabbitMqConsumerSettings> consumerSettings,
    ILogger<RabbitMqBackgroundService> logger)
    : BackgroundService
{
    private IModel? channel;

    private IConnection? connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.connection = await client.TryConnectAsync(consumerSettings.Value.ConnectionAttemptCount, stoppingToken);
        if (this.connection is null)
        {
            logger.LogInformation("Listening RabbitMQ events wasn't started");
            return;
        }

        this.channel = this.connection!.CreateModel();
        foreach (var initializer in initializers)
        {
            initializer.Initialize(this.channel);
        }

        logger.LogInformation(
            "Listening RabbitMQ events has started on {Address}. Queue name is {Queue}. Consumer mode is {Mode}",
            serverSettings.Value.Address,
            consumerSettings.Value.Queue,
            consumerSettings.Value.Mode);

        await consumer.ConsumeAsync(this.channel!, stoppingToken);
    }

    public override void Dispose()
    {
        consumer.Dispose();
        this.channel?.Close();
        this.connection?.Close();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
