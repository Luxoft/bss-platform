using System.Text;
using System.Text.Json;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Interfaces;
using Bss.Platform.Events.Models;
using Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.Events.Internal;

internal sealed class RabbitEventSchemaExportInitializer(RabbitEventsSchemaExporter exporter) : IRabbitInitializer
{
    public Task InitializeAsync(IModel model, CancellationToken cancellationToken)
    {
        exporter.Export(model, cancellationToken);
        return Task.CompletedTask;
    }
}
