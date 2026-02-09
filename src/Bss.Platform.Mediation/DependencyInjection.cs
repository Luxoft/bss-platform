using System.Reflection;

using Bss.Platform.Mediation.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.Mediation;

public static class DependencyInjection
{
    public static IServiceCollection AddMediation(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Mediator>();

        services.Scan(s => s
                           .FromAssemblies(assemblies)
                           .AddClasses(c => c.AssignableTo(typeof(IRequestHandler<,>)))
                           .AsImplementedInterfaces()
                           .WithScopedLifetime()
                           .AddClasses(c => c.AssignableTo(typeof(IPipelineBehavior<,>)))
                           .AsImplementedInterfaces()
                           .WithScopedLifetime());

        return services;
    }
}
