using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bss.Platform.RabbitMq.Schemas;

public record RabbitSchemaSource(string Exchange, string? Queue = null);

public record RabbitSchemaMessage(
    [property: JsonPropertyName("input")] JsonElement Input,
    [property: JsonPropertyName("output")] JsonElement Output,
    [property: JsonPropertyName("systemName")] string SystemName,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("queue")] string? Queue,
    [property: JsonPropertyName("schemaHash")] string SchemaHash);
