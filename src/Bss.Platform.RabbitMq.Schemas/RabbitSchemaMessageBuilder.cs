using System.Security.Cryptography;
using System.Text.Json;

namespace Bss.Platform.RabbitMq.Schemas;

public class RabbitSchemaMessageBuilder(RabbitEventsSchemaGenerator generator)
{
    public virtual RabbitSchemaMessage Build(IRabbitSchemaTypeProvider types, RabbitSchemaSource source, string systemName)
    {
        using var input = JsonDocument.Parse(generator.GenerateSchema(types.InputEvents).ToJson());
        using var output = JsonDocument.Parse(generator.GenerateSchema(types.OutputEvents).ToJson());
        var inputSchema = Normalize(input.RootElement);
        var outputSchema = Normalize(output.RootElement);
        var content = JsonSerializer.SerializeToElement(new { input = inputSchema, output = outputSchema });
        var hash = Convert.ToHexStringLower(SHA256.HashData(CanonicalBytes(content)));
        return new RabbitSchemaMessage(inputSchema, outputSchema, systemName, source.Exchange, source.Queue, hash);
    }

    private static JsonElement Normalize(JsonElement element)
    {
        using var document = JsonDocument.Parse(CanonicalBytes(element));
        return document.RootElement.Clone();
    }

    private static byte[] CanonicalBytes(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(element, writer);
        return stream.ToArray();
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                Write(property.Value, writer);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) Write(item, writer);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
