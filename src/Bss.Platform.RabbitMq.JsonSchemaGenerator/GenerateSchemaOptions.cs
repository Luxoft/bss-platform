namespace Bss.Platform.RabbitMq.JsonSchemaGenerator;

public class GenerateSchemaOptions
{
    public string Path { get; set; } = "/api/rabbit-json-schema";

    /// <summary>
    ///     Enable exporting types for consumed messages registered with method AddPlatformRabbitMqConsumerWithMessages
    ///     <see cref="Bss.Platform.RabbitMq.Consumer.DependencyInjection" />
    /// </summary>
    public bool ExportConsumingTypes { get; set; } = true;

    /// <summary>
    ///     Function to get maps RoutingKey-Type for consuming events registered by old way / handled in processor,
    ///     work with <see cref="ExportConsumingTypes"/> but with higher priority (by uniq RoutingKey)
    ///     <see cref="Bss.Platform.RabbitMq.Consumer.Interfaces.IRabbitMqMessageProcessor" />
    /// </summary>
    public Func<IEnumerable<(string RoutingKey, Type EventType)>>? ManualRegisteredConsumedEventsFn { get; set; }

    /// <summary>
    ///     Function to get produced event types
    /// </summary>
    public Func<IEnumerable<Type>>? ProducedEventTypes { get; set; }

    /// <summary>
    ///     System prefix for type name
    /// </summary>
    public string TypePrefix { get; set; } = string.Empty;

    /// <summary>
    ///     Function overrides <see cref="TypePrefix" /> and <see cref="ProducedEventTypes" /> to manual map
    ///     RoutingKey/EventName and types
    /// </summary>
    public Func<IEnumerable<(string RoutingKey, Type EventType)>>? OutputEventMappingFn { get; set; }
}
