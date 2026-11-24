using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Base;
using Bss.Platform.RabbitMq.Services;
using Bss.Platform.RabbitMq.Schemas;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bss.Platform.RabbitMq;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformRabbitMqClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMqBase();
        services.AddRabbitMqSchemas();
        services.Configure<RabbitSchemaOptions>(configuration.GetSection("RabbitMQ:Schemas"));
        services.TryAddSingleton<IRabbitSchemaTypeProvider, PlatformSchemaTypeProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRabbitInitializer, ExportSchemasInitializer>());
        services.AddOptions<OutputEventCatalog>();
        services.TryAddSingleton<IOutputEventCatalog>(sp => sp.GetRequiredService<IOptions<OutputEventCatalog>>().Value);
        var producerSection = configuration.GetSection("RabbitMQ:Producer");
        services.AddOptions<RabbitMqProducerSettings>().Bind(producerSection).Configure(settings =>
        {
            // The standard dictionary binder skips null/empty entries. Those entries mean
            // "bind defaults" here, and an explicitly empty dictionary differs from absence.
            if (producerSection.GetChildren().Any(x => x.Key.Equals(nameof(settings.SystemBindings), StringComparison.OrdinalIgnoreCase)))
            {
                settings.SystemBindings = producerSection.GetSection(nameof(settings.SystemBindings)).GetChildren()
                    .ToDictionary(x => x.Key, x => x.Get<string[]>(), StringComparer.Ordinal);
            }
        });
        services.TryAddSingleton<IOutputBindingsResolver, OutputBindingsResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRabbitInitializer, OutputQueuesInitializer>());
        services.Configure<RabbitMqServerSettings>(configuration.GetSection("RabbitMQ:Server"));
        services.AddOptions<RabbitMqSettings>()
            .Bind(configuration.GetSection("RabbitMQ"))
            .PostConfigure<IServiceProvider>((options, provider) =>
            {
                var consumer = provider.GetService<IRabbitInputEventProvider>();
                var legacy = consumer?.Exchange ?? configuration["RabbitMQ:Consumer:Exchange"];
                if (!string.IsNullOrWhiteSpace(legacy) && !string.IsNullOrWhiteSpace(options.Exchange)
                    && !string.Equals(legacy, options.Exchange, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("RabbitMQ:Exchange and RabbitMQ:Consumer:Exchange must match.");
                }

                options.Exchange ??= legacy;
                options.ConnectionAttemptCount ??= consumer?.ConnectionAttemptCount ?? configuration.GetValue<int?>("RabbitMQ:Consumer:ConnectionAttemptCount");
            });
        services.TryAddSingleton<IRabbitMqClient, RabbitMqClient>();
        services.TryAddSingleton<RabbitMqInitializationService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RabbitMqInitializationHost>());
        return services;
    }

    public static IServiceCollection RegisterRabbitMqOutputEvents(this IServiceCollection services, Action<OutputEventCatalog> setup)
    {
        services.Configure(setup);
        return services;
    }
}

internal sealed class RabbitMqInitializationHost(RabbitMqInitializationService initialization) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => initialization.EnsureInitializedAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
