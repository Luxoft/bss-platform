using Bss.Platform.RabbitMq.Consumer.BackgroundServices;
using Bss.Platform.RabbitMq.Consumer.Enums;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Services;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.RabbitMq.Consumer;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformRabbitMqConsumer<TMessageProcessor>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TMessageProcessor : class, IRabbitMqMessageProcessor
    {
        var consumerSettingsSection = configuration.GetSection("RabbitMQ:Consumer");
        if (consumerSettingsSection.Get<RabbitMqConsumerSettings>()!.Mode == ConsumerMode.MultipleActiveConsumers)
        {
            services.AddSingleton<IRabbitMqConsumer, ConcurrentConsumer>();
        }
        else
        {
            services.AddSingleton<IRabbitMqConsumer, SynchronizedConsumer>();
        }

        return services
               .Configure<RabbitMqConsumerSettings>(consumerSettingsSection)
               .AddSingleton<IRabbitMqMessageReader, MessageReader>()
               .AddSingleton<IDeadLetterProcessor, DeadLetterProcessor>()
               .AddSingleton<IRabbitMqMessageProcessor, TMessageProcessor>()
               .AddSingleton<IRabbitMqInitializer, ConsumerInitializer>()
               .AddHostedService<RabbitMqBackgroundService>();
    }

    public static IServiceCollection AddPlatformRabbitMqSqlServerConsumerLock(this IServiceCollection services, string connectionString) =>
        services
            .AddSingleton<IRabbitMqConsumerLockService, MsSqlLockService>()
            .AddSingleton(new SqlConnectionStringProvider(connectionString));
}
