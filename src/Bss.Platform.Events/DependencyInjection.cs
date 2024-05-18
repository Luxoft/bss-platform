using System.Data;
using System.Reflection;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;
using Bss.Platform.Events.Models;
using Bss.Platform.Events.Publishers;

using DotNetCore.CAP;
using DotNetCore.CAP.Internal;

using Microsoft.Extensions.DependencyInjection;

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
        where TEventProcessor : class, IIntegrationEventProcessor
    {
        services
            .AddSingleton<IIntegrationEventProcessor, TEventProcessor>()
            .AddSingleton<IConsumerServiceSelector, CapConsumerServiceSelector>(x => new CapConsumerServiceSelector(x, eventsAssembly))
            .AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>()
            .AddScoped<ICapTransaction>(
                serviceProvider =>
                {
                    var capTransaction = ActivatorUtilities.CreateInstance<SqlServerCapTransaction>(serviceProvider);
                    capTransaction.DbTransaction = serviceProvider.GetRequiredService<IDbTransaction>();
                    return capTransaction;
                })
            .AddCap(
                x =>
                {
                    var eventsOptions = IntegrationEventsOptions.Default;
                    setup?.Invoke(eventsOptions);

                    x.FailedRetryCount = eventsOptions.FailedRetryCount;
                    x.SucceedMessageExpiredAfter = (int)TimeSpan.FromDays(eventsOptions.RetentionDays).TotalSeconds;

                    x.UseSqlServer(
                        o =>
                        {
                            o.ConnectionString = eventsOptions.SqlServer.ConnectionString;
                            o.Schema = eventsOptions.SqlServer.Schema;
                        });

                    x.UseDashboard(o => o.PathMatch = eventsOptions.DashboardPath);

                    if (!eventsOptions.MessageQueue.Enable)
                    {
                        x.UseInMemoryMessageQueue();
                        return;
                    }

                    x.DefaultGroupName = eventsOptions.MessageQueue.ExchangeName;
                    x.UseRabbitMQ(
                        o =>
                        {
                            o.HostName = eventsOptions.MessageQueue.Host;
                            o.Port = eventsOptions.MessageQueue.Port;
                            o.VirtualHost = eventsOptions.MessageQueue.VirtualHost;
                            o.Password = eventsOptions.MessageQueue.Secret;
                            o.UserName = eventsOptions.MessageQueue.UserName;
                            o.ExchangeName = eventsOptions.MessageQueue.ExchangeName;
                            o.BasicQosOptions = new RabbitMQOptions.BasicQos(1, true);
                        });
                });

        return services;
    }
}
