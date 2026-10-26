using System.Reflection;

namespace Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

public interface IRabbitSchemaExportSettings
{
    string RoutingKey => "RabbitEventSchemas";

    string ToQueueName => "ToDocumenter";

    string System => SystemEntryAssemblyName;

    bool IsEnabled => true;

    static string SystemEntryAssemblyName => Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    string FromQueueName { get; }

    string ExchangeName { get; }

    IReadOnlyDictionary<string, Type> InputEvents { get; }

    IReadOnlyDictionary<string, Type> OutputEvents { get; }
}

public abstract class RabbitSchemaExportSettingsBase(
    //[FromKeyedServices(InputEventTypeKey)]
    Dictionary<string, Type>? consumedTypes = null) : IRabbitSchemaExportSettings
{
    public const string InputEventTypeKey = nameof(InputEventTypeKey);

    public abstract string FromQueueName { get; }

    public abstract string ExchangeName { get; }

    public IReadOnlyDictionary<string, Type> InputEvents => consumedTypes ?? [];

    public IReadOnlyDictionary<string, Type> OutputEvents => throw new NotImplementedException();
}
