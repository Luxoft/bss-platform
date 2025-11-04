using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.RabbitMq.JsonSchemaGenerator;

public static class DependencyInjection
{
    public static IApplicationBuilder UseRabbitJsonSchemaGenerator(
        this IApplicationBuilder app,
        Action<GenerateSchemaOptions>? setup = null)
    {
        var options = new GenerateSchemaOptions();
        setup?.Invoke(options);

        var consumedEvents =
            app.ApplicationServices
                .GetKeyedService<Dictionary<string, Type>>(Consumer.DependencyInjection.RoutingMessageProviderKey)
                ?.Select(x => (x.Key, x.Value))
            ?? [];

        var producedEvents = options.ProducedEventTypes.Select(x => ($"{options.SystemPrefix}{x.Name}", x));

        var allEventsDict = producedEvents.Concat(consumedEvents)
            .DistinctBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Item1, x => x.Item2);

        return app.UseMiddleware<GenerateSchemaMiddleware>(options.Path, allEventsDict);
    }
}
