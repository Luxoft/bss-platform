using System.Reflection;

using Bss.Platform.Mediation.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.Mediation;

public static class DependencyInjection
{
    public static IServiceCollection AddMediation(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Mediator>();

        services.Scan(scan => scan
                              .FromAssemblies(assemblies)
                              .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)), publicOnly: false)
                              .AsImplementedInterfaces()
                              .WithScopedLifetime()
                              .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>)), publicOnly: false)
                              .AsImplementedInterfaces()
                              .WithScopedLifetime()
                              .AddClasses(classes => classes.AssignableTo(typeof(INotificationHandler<>)), publicOnly: false)
                              .AsImplementedInterfaces()
                              .WithScopedLifetime()
                              .AddClasses(classes => classes.AssignableTo(typeof(IPipelineBehavior<,>)), publicOnly: false)
                              .AsImplementedInterfaces()
                              .WithScopedLifetime()
                              .AddClasses(classes => classes.AssignableTo(typeof(IPipelineBehavior<>)), publicOnly: false)
                              .AsImplementedInterfaces()
                              .WithScopedLifetime());

        return services;
    }
}
