using System.Reflection;

namespace Bss.Platform.Events.Models;

public class SchemaExportSettings
{
    public string QueueName { get; set; } = "ToDocumenter";

    public string RoutingKey { get; set; } = "RabbitEventSchemas";

    public string System { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
}
