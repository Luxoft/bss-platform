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

        var consumedEventMappingFn = options.ManualRegisteredConsumedEventsFn ?? (() => []);
        if (options.ExportConsumingTypes)
        {
            consumedEventMappingFn = () =>
            {
                var manualPassedMessages = options.ManualRegisteredConsumedEventsFn?.Invoke() ?? [];
                var autoRegisteredMessages = app.ApplicationServices
                    .GetKeyedService<Dictionary<string, Type>>(Consumer.DependencyInjection.RoutingMessageProviderKey) ?? [];

                return manualPassedMessages
                    .Concat(autoRegisteredMessages.Select(x => (x.Key, x.Value)))
                    .DistinctBy(x => x.Item1, StringComparer.OrdinalIgnoreCase);
            };
        }

        var producedEventMappingFn = options.OutputEventMappingFn ?? (() => []);
        if (options.ProducedEventTypes != null && options.OutputEventMappingFn == null)
        {
            producedEventMappingFn = () => options.ProducedEventTypes.Invoke().Select(x => ($"{options.TypePrefix}{x.Name}", x));
        }

        return app.UseMiddleware<GenerateSchemaMiddleware>(options.Path, consumedEventMappingFn, producedEventMappingFn);
    }
}
