using Bss.Platform.RabbitMq.Base;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bss.Platform.RabbitMq.Schemas;

public static class DependencyInjection
{
    /// <summary>Adds standalone schema services without a platform client, consumer or startup host.</summary>
    public static IServiceCollection AddRabbitMqSchemas(this IServiceCollection services, Action<RabbitSchemaOptions>? setup = null)
    {
        services.AddRabbitMqBase();
        var options = services.AddOptions<RabbitSchemaOptions>()
            .Validate(x => !x.Enabled || (!string.IsNullOrWhiteSpace(x.QueueName) && !string.IsNullOrWhiteSpace(x.RoutingKey)
                && !string.IsNullOrWhiteSpace(x.SystemName)), "Schema destination and system name must be valid.");
        if (setup is not null) options.Configure(setup);
        services.TryAddSingleton<RabbitEventsSchemaGenerator>();
        services.TryAddSingleton<RabbitSchemaMessageBuilder>();
        services.TryAddSingleton<IRabbitSchemaSender, RabbitSchemaSender>();
        return services;
    }
}
