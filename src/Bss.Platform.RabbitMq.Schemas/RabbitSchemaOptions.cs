using System.Reflection;

namespace Bss.Platform.RabbitMq.Schemas;

public class RabbitSchemaOptions
{
    public bool Enabled { get; set; } = true;
    public bool ThrowOnFailure { get; set; }
    public string SystemName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
    public string QueueName { get; set; } = "ToDocumenter";
    public string RoutingKey { get; set; } = "RabbitEventSchemas";
    public string? SourceQueueName { get; set; }
    public TimeSpan ConfirmationTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
