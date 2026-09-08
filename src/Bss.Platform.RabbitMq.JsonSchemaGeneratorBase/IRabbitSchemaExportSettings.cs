using System.Reflection;

namespace Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

public interface IRabbitSchemaExportSettings
{
    string RoutingKey => "RabbitEventSchemas";

    string ToQueueName => "ToDocumenter";

    string System => Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    string FromQueueName { get; }

    string ExchangeName { get; }

    IReadOnlyDictionary<string, Type> InputEvents { get; }

    IReadOnlyDictionary<string, Type> OutputEvents { get; }
}
