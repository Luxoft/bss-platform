using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Services;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bss.Platform.RabbitMq;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformRabbitMqClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMqBase();
        services.Configure<RabbitMqServerSettings>(configuration.GetSection("RabbitMQ:Server"));
        services.AddOptions<RabbitMqSettings>()
            .Bind(configuration.GetSection("RabbitMQ"))
            .PostConfigure(options =>
            {
                var legacy = configuration["RabbitMQ:Consumer:Exchange"];
                if (!string.IsNullOrWhiteSpace(legacy) && !string.IsNullOrWhiteSpace(options.Exchange)
                    && !string.Equals(legacy, options.Exchange, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("RabbitMQ:Exchange and RabbitMQ:Consumer:Exchange must match.");
                }

                options.Exchange ??= legacy;
                options.ConnectionAttemptCount ??= configuration.GetValue<int?>("RabbitMQ:Consumer:ConnectionAttemptCount");
            });
        services.TryAddSingleton<IRabbitMqClient, RabbitMqClient>();
        services.TryAddSingleton<RabbitMqInitializationService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RabbitMqInitializationHost>());
        return services;
    }
}

internal sealed class RabbitMqInitializationHost(RabbitMqInitializationService initialization) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => initialization.EnsureInitializedAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
