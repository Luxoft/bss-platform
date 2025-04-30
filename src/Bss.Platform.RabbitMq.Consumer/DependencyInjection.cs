using Bss.Platform.RabbitMq.Consumer.BackgroundServices;
using Bss.Platform.RabbitMq.Consumer.Enums;
using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Internal;
using Bss.Platform.RabbitMq.Consumer.Services;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.RabbitMq.Consumer;

public static class DependencyInjection
{
    public const string RoutingMessageProviderKey = nameof(RoutingMessageProviderKey);
    
    public static IServiceCollection AddPlatformRabbitMqSqlServerConsumerLock(this IServiceCollection services, string connectionString) =>
        services
            .AddSingleton<IRabbitMqConsumerLockService, MsSqlLockService>()
            .AddSingleton(new SqlConnectionStringProvider(connectionString));

    /// <summary>
    ///     Old way register processor, with manual deserialization, low-level rabbit event handling
    /// </summary>
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

    /// <summary>
    ///     Add consumer with default serialization (case in-sensitive) for added messages/routing keys (register via builder)
    /// </summary>
    public static IServiceCollection AddPlatformRabbitMqConsumerWithMessages<TProcessor, TEvent>(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IMessageRegister<TEvent>, IMessageRegister<TEvent>> setup)
        where TProcessor : class, IRabbitMqEventProcessor<TEvent>
    {
        var internalBuilder = new RabbitMqBuilder<TEvent>();
        setup(internalBuilder);

        return services.AddRabbitAndEvents<TProcessor, TEvent>(configuration, internalBuilder.RegisteredMessages);
    }
    
    /// <summary>
    ///     Add consumer with default serialization (case in-sensitive), and find and register events marked by attribute
    /// </summary>
    public static IServiceCollection AddPlatformRabbitMqConsumerWithMessages<TProcessor, TEvent, TAttribute>(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<TAttribute, string> getRoutingKey)
        where TProcessor : class, IRabbitMqEventProcessor<TEvent>
        where TAttribute : Attribute
    {
        var messages = new AttributeMessageLinkProvider<TAttribute>(getRoutingKey).Find();

        return services.AddRabbitAndEvents<TProcessor, TEvent>(configuration, messages);
    }

    private static IServiceCollection AddRabbitAndEvents<TProcessor, TEvent>(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<MessageLink> messages)
        where TProcessor : class, IRabbitMqEventProcessor<TEvent>
    {
        services.AddSingleton<IRabbitMqEventProcessor<TEvent>, TProcessor>();
        services.AddPlatformRabbitMqConsumer<RabbitMqMessageProcessor<TEvent>>(configuration);

        var routeMessages = messages.ToDictionary(
            x => x.RoutingKey,
            x => x.MessageType.IsAssignableTo(typeof(TEvent))
                ? x.MessageType
                : throw new ArgumentException(
                    $"Unexpected message type '{x.MessageType.Name}' with routing key '{x.RoutingKey}', allow only {typeof(TEvent).Name} and its subtypes"),
            StringComparer.OrdinalIgnoreCase);

        services.AddKeyedSingleton(RoutingMessageProviderKey, routeMessages);
        services.PostConfigure<RabbitMqConsumerSettings>(
            opts =>
            {
                if (opts.RoutingKeys.Length > 0)
                {
                    const string notEmptyRoutingKeysConfigError =
                        $"""
                         '{nameof(opts.RoutingKeys)}' in '{nameof(RabbitMqConsumerSettings)}' should be empty,
                         when you use '{nameof(AddPlatformRabbitMqConsumerWithMessages)}', check your configuration 
                         """;

                    throw new InvalidOperationException(notEmptyRoutingKeysConfigError);
                }

                opts.RoutingKeys = routeMessages.Keys.ToArray();
            }
        );

        return services;
    }
}
