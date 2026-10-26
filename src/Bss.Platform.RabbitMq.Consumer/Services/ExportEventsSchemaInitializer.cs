using System.Collections.ObjectModel;

using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;
using Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Bss.Platform.RabbitMq.Consumer.Services;

public sealed class ExportEventsSchemaInitializer(
    [FromKeyedServices(ExportEventsSchemaInitializer.SettingsKey)]
    IRabbitSchemaExportSettings settings)
    : IRabbitMqInitializer
{
    public const string SettingsKey = $"{nameof(ExportEventsSchemaInitializer)}.{nameof(SettingsKey)}";

    public void Initialize(IModel model)
    {
        new RabbitEventsSchemaExporter(settings).Export(model);
    }

    internal sealed class RabbitSchemaExportSettings(
        IOptions<RabbitMqConsumerSettings> options,
        [FromKeyedServices(RabbitSchemaExportSettings.InputEventTypeKey)]
        Dictionary<string, Type>? inputTypes = null,
        [FromKeyedServices(DependencyInjection.RoutingConsumedMessagesProviderKey)]
        Dictionary<string, Type>? inputAutoTypes = null,
        [FromKeyedServices(RabbitSchemaExportSettings.OutputEventTypeKey)]
        Dictionary<string, Type>? outputTypes = null,
        [FromKeyedServices(RabbitSchemaExportSettings.SystemNameKey)]
        string? systemName = null) : IRabbitSchemaExportSettings
    {
        public const string InputEventTypeKey = $"{nameof(RabbitSchemaExportSettings)}.{nameof(InputEventTypeKey)}";

        public const string OutputEventTypeKey = $"{nameof(RabbitSchemaExportSettings)}.{nameof(OutputEventTypeKey)}";

        public const string SystemNameKey = $"{nameof(RabbitSchemaExportSettings)}.{nameof(SystemNameKey)}";

        public string FromQueueName => options.Value.Queue;

        public string ExchangeName => options.Value.Exchange;

        public string System => systemName ?? IRabbitSchemaExportSettings.SystemEntryAssemblyName;

        public bool IsEnabled => inputTypes != null || inputAutoTypes != null || outputTypes != null;

        public IReadOnlyDictionary<string, Type> InputEvents => inputTypes ?? inputAutoTypes ?? [];

        public IReadOnlyDictionary<string, Type> OutputEvents => outputTypes ?? [];
    }
}
