using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bss.Platform.RabbitMq.Base;

public static class DependencyInjection
{
    public static IServiceCollection AddRabbitMqBase(this IServiceCollection services)
    {
        services.TryAddSingleton<IQueueDeclarationPolicy, QuorumQueueDeclarationPolicy>();
        return services;
    }

    public static IServiceCollection RemoveRabbitMqInitializer<TInitializer>(this IServiceCollection services)
        where TInitializer : class, IRabbitInitializer
    {
        foreach (var descriptor in services.Where(x => x.ServiceType == typeof(IRabbitInitializer)
                     && (x.ImplementationType == typeof(TInitializer) || x.ImplementationInstance is TInitializer)).ToArray())
        {
            services.Remove(descriptor);
        }

        return services;
    }
}
