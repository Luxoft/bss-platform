using System.Data;
using System.Reflection;
using System.Text.Json;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;
using Bss.Platform.Events.Internal;
using Bss.Platform.Events.Models;
using Bss.Platform.Events.Publishers;

using DotNetCore.CAP;
using DotNetCore.CAP.Filter;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Serialization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Savorboard.CAP.InMemoryMessageQueue;

namespace Bss.Platform.Events;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformDomainEvents(this IServiceCollection services) =>
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();

    public static IServiceCollection AddPlatformIntegrationEvents<TEventProcessor>(
        this IServiceCollection services,
        Assembly eventsAssembly,
        Action<IntegrationEventsOptions>? setup = null)
        where TEventProcessor : class, IIntegrationEventProcessor, IIntegrationEventProcessor<IIntegrationEvent>
    {
        services
            .AddSingleton<IIntegrationEventProcessor, TEventProcessor>()
            .AddSingleton<IConsumerServiceSelector, CapConsumerServiceSelectorLegacy>(x => new(x, eventsAssembly));
        services.AddPlatformIntegrationEventsInternal<IIntegrationEvent, IntegrationEventsOptions>(SetLegacyQueueNameWithVersion(setup));
        services.TryAddLegacyEventPublisher();

        return services;
    }

    /// <summary>
    /// A new way to register integration events, required to set up internal and external events<br/>
    /// Used <see cref="Bss.Platform.Mediation.Abstractions.IMediator">Bss.Platform.Mediation</see>
    /// </summary>
    /// <returns>Automatically registered Legacy IIntegrationEventPublisher</returns>
    public static IServiceCollection AddPlatformIntegrationEvents<TEventProcessor>(
        this IServiceCollection services,
        Action<IIntegrationEventSetup<IIntegrationEvent, IIntegrationEvent>> setupEvents,
        Action<RabbitIntegrationEventsOptions> setupOptions)
        where TEventProcessor : class, IIntegrationEventProcessor<IIntegrationEvent> =>
        services
            .AddPlatformIntegrationEvents<TEventProcessor, IIntegrationEvent, IIntegrationEvent>(setupEvents, setupOptions)
            .TryAddLegacyEventPublisher();

    /// <summary>
    /// A new way to register integration events, required to set up internal and external events
    /// </summary>
    /// <returns>Automatically registered IIntegrationEventPublisher&lt;TEvent&gt; wrap it if you need</returns>
    public static IServiceCollection AddPlatformIntegrationEvents<TEventProcessor, TEvent>(
        this IServiceCollection services,
        Action<IIntegrationEventSetup<TEvent, TEvent>> setupEvents,
        Action<RabbitIntegrationEventsOptions> setupOptions)
        where TEventProcessor : class, IIntegrationEventProcessor<TEvent>
        where TEvent : class =>
        services.AddPlatformIntegrationEvents<TEventProcessor, TEvent, TEvent>(setupEvents, setupOptions);

    /// <summary>
    /// A new way to register integration events, required to set up internal and external events
    /// </summary>
    /// <returns>Automatically registered IIntegrationEventPublisher&lt;TEvent&gt; wrap it if you need</returns>
    public static IServiceCollection AddPlatformIntegrationEvents<TEventProcessor, TInputEvent, TOutputEvent>(
        this IServiceCollection services,
        Action<IIntegrationEventSetup<TInputEvent, TOutputEvent>> setupEvents,
        Action<RabbitIntegrationEventsOptions> setupOptions)
        where TEventProcessor : class, IIntegrationEventProcessor<TInputEvent>
        where TInputEvent : class
        where TOutputEvent : notnull
    {
        // TODO: via configuration
        var typeProvider = new EventTypeProvider<TInputEvent,TOutputEvent>();
        setupEvents.Invoke(typeProvider);

        services
            .AddSingleton<IEventTypeProvider>(typeProvider)
            .AddSingleton<IConsumerServiceSelector, CapConsumerServiceSelectorNew>();
        var eventsOptions = services.AddPlatformIntegrationEventsInternal<TInputEvent, RabbitIntegrationEventsOptions>(setupOptions);
        services.Configure((RabbitMQOptions opt) =>
            {
                // NOTE: required for rabbit messages generated outside of CAP
                opt.CustomHeadersBuilder = (msg, sp) =>
                [
                    new(Headers.MessageId, sp.GetRequiredService<ISnowflakeId>().NextId().ToString()),
                    new(Headers.MessageName, msg.RoutingKey),
                    new(Headers.Type, typeof(TInputEvent).Name)
                ];
            })
            .TryAddScoped<IIntegrationEventPublisher<TOutputEvent>, IntegrationEventPublisherNew<TOutputEvent>>();

        services.AddSingleton<IIntegrationEventProcessor<TInputEvent>, TEventProcessor>();
        // NOTE: register TEventProcessor for each type (required for CapConsumerExecutor<TEvent>)
        typeProvider.InputEvents.Keys
            .Select(t => typeof(IIntegrationEventProcessor<>).MakeGenericType(t))
            .ToList()
            .ForEach(x => services.AddSingleton(x, sp => sp.GetRequiredService<IIntegrationEventProcessor<TInputEvent>>()));

        services.AddSingleton<IRabbitInitializer, RabbitEventSchemaExportInitializer>();

        if (eventsOptions.UseFailedEventProcessor)
        {
            services.AddSingleton<DeadLetterProcessor>();
            var (exchange, queue) = eventsOptions.DeadLetterOptions;
            services.AddSingleton<IRabbitInitializer>(new DeadLetterBindingsInitializer(exchange, queue));
            services.AddSingleton<IFailedEventProcessor<TInputEvent>>(sp => sp.GetRequiredService<DeadLetterProcessor>());
            services.RemoveAll(typeof(ISerializer));
            services.AddSingleton<ISerializer, RawCapturingSerializer>();
            services.AddScoped<ISubscribeFilter, CapExceptionFilter<TInputEvent>>();
        }

        if (eventsOptions.MessageQueue.Enable)
        {
            services.AddExternalSystemQueueBindings(eventsOptions.MessageQueue.ExternalSystemBindingsSectionPath);
            services.AddSingleton<IExternalSystemBindingsResolver, ExternalSystemBindingsResolver>();
            services.AddSingleton<IRabbitInitializer, ExternalSystemQueueBindingsInitializer>();
            if (eventsOptions.MessageQueue.EnableSchemaExport)
            {
                services.AddSingleton<IRabbitInitializer, RabbitEventSchemaExportInitializer>();
            }

            services.AddHostedService<RabbitInitializersHostedService>();
        }
        return services;
    }

    private static IServiceCollection TryAddLegacyEventPublisher(this IServiceCollection services)
    {
        services.TryAddScoped<IIntegrationEventPublisher, IntegrationEventPublisherLegacy>();
        services.TryAddScoped<IIntegrationEventPublisher<IIntegrationEvent>>(sp => sp.GetRequiredService<IIntegrationEventPublisher>());
        return services;
    }

    /// <summary>
    /// requiered for backward compatibility, added postfix ".v1" to queue name, like origin cap behavior without <see cref="CapConsumerServiceSelectorNew" />
    /// </summary>
    private static Action<IntegrationEventsOptions> SetLegacyQueueNameWithVersion(Action<IntegrationEventsOptions>? setupOptions) =>
        opt =>
        {
            setupOptions?.Invoke(opt);
            var originMessageQueueName = opt.MessageQueue.QueueName;
            opt.MessageQueue.QueueName = string.IsNullOrWhiteSpace(originMessageQueueName) ? $"{opt.MessageQueue.ExchangeName}.v1" : originMessageQueueName;
        };

    private static TOptions AddPlatformIntegrationEventsInternal<TInputEvent, TOptions>(
        this IServiceCollection services,
        Action<TOptions>? setupEventOptions = null)
        where TInputEvent : class
        where TOptions : IntegrationEventsOptions, new()
    {
        var eventsOptions = new TOptions();
        setupEventOptions?.Invoke(eventsOptions);
        setupEventOptions ??= _ => { };
        services.Configure(setupEventOptions);

        services
            .AddScoped<ICapTransaction>(serviceProvider =>
            {
                var capTransaction = ActivatorUtilities.CreateInstance<SqlServerCapTransaction>(serviceProvider);
                capTransaction.DbTransaction = serviceProvider.GetRequiredService<IDbTransaction>();
                return capTransaction;
            })
            .AddCap(x =>
            {
                x.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                x.FailedRetryCount = eventsOptions.FailedRetryCount;
                x.SucceedMessageExpiredAfter = (int)TimeSpan.FromDays(eventsOptions.RetentionDays).TotalSeconds;

                if (!string.IsNullOrEmpty(eventsOptions.SqlServer.ConnectionString))
                {
                    x.UseSqlServer(o =>
                    {
                        o.ConnectionString = eventsOptions.SqlServer.ConnectionString;
                        o.Schema = eventsOptions.SqlServer.Schema;
                    });
                }

                x.UseDashboard(o =>
                {
                    o.PathMatch = eventsOptions.DashboardPath;
                    o.PathBase = eventsOptions.GatewayPrefix;
                    if (eventsOptions.AuthorizationPredicate is { } authPredicate)
                    {
                        o.AllowAnonymousExplicit = false;
                        o.AuthorizationPolicy = AddDashboardAuthorizationPolicy(services, authPredicate);
                    }
                });

                var rabbitSettings = eventsOptions.MessageQueue;
                if (rabbitSettings.Enable)
                {
                    x.DefaultGroupName = string.IsNullOrWhiteSpace(rabbitSettings.QueueName)
                        ? rabbitSettings.ExchangeName
                        : rabbitSettings.QueueName;

                    x.UseRabbitMQ(o =>
                    {
                        o.HostName = rabbitSettings.Host;
                        o.Port = rabbitSettings.Port;
                        o.VirtualHost = rabbitSettings.VirtualHost;
                        o.Password = rabbitSettings.Secret;
                        o.UserName = rabbitSettings.UserName;
                        o.ExchangeName = rabbitSettings.ExchangeName;
                        o.BasicQosOptions = new(1, true);
                    });
                }
                else
                {
                    x.UseInMemoryMessageQueue();
                }

                eventsOptions.OverrideCapOptions?.Invoke(x);
            });

        return eventsOptions;
    }

    private static void AddExternalSystemQueueBindings(this IServiceCollection services, string? sectionPath)
    {
        var optionsBuilder = services.AddOptions<ExternalSystemBindingsOptions>();
        if (!string.IsNullOrWhiteSpace(sectionPath))
        {
            optionsBuilder.BindConfiguration(sectionPath);
            // ConfigurationBinder drops Dictionary entries whose bound value is null, and both `null` and `[]`
            // bind to null for an array value - so BindConfiguration above silently loses SystemBindings keys
            // that have no explicit routing keys. Rebuild that dictionary from IConfiguration directly instead.
            // Uses Configure (not PostConfigure) so consumers can still override via PostConfigure regardless
            // of call order relative to this setup method - PostConfigure always runs after every Configure.
            optionsBuilder.Configure<IConfiguration>((options, configuration) =>
                                                         options.SystemBindings = ResolveSystemBindings(configuration, sectionPath));
        }
    }

    internal static Dictionary<string, string[]?> ResolveSystemBindings(IConfiguration configuration, string sectionPath) =>
        configuration
            .GetSection(sectionPath)
            .GetSection(nameof(ExternalSystemBindingsOptions.SystemBindings))
            .GetChildren()
            .ToDictionary(section => section.Key, section => section.Get<string[]>());

    private static string AddDashboardAuthorizationPolicy(IServiceCollection services, Func<HttpContext, Task<bool>> authPredicate)
    {
        const string policyName = "bss-platform-events-dashboard-auth";
        services.AddAuthorizationBuilder()
            .AddPolicy(
                policyName,
                policy => policy.RequireAssertion(ctx =>
                {
                    var httpContext = ctx.Resource as HttpContext
                                      ?? (ctx.Resource as AuthorizationFilterContext)?.HttpContext
                                      ?? throw new("Can't authorize, http context is not available");

                    return authPredicate(httpContext);
                }));

        return policyName;
    }
}
