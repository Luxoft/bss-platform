namespace Bss.Platform.RabbitMq.JsonSchemaGenerator;

public class GenerateSchemaOptions
{
    public string Path { get; set; } = "/api/rabbit-json-schema";


    /// <summary>
    ///     Collection of produced events
    /// </summary>
    public IEnumerable<Type> ProducedEventTypes { get; set; } = [];

    /// <summary>
    ///     Prefix to add to produced event type
    /// </summary>
    public string SystemPrefix { get; set; } = string.Empty;
}
