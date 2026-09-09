using Bss.Platform.Events.Interfaces;
using Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

using RabbitMQ.Client;

namespace Bss.Platform.Events.Internal;

public sealed class RabbitExportEventsSchemaInitializer(RabbitEventsSchemaExporter exporter) : IRabbitInitializer
{
    public Task InitializeAsync(IModel model, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        exporter.Export(model);
        return Task.CompletedTask;
    }
}
