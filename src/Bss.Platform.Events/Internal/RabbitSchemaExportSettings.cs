using System.Reflection;

using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Models;
using Bss.Platform.RabbitMq.JsonSchemaGeneratorBase;

using DotNetCore.CAP;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Events.Internal;

internal class RabbitSchemaExportSettings(
    IOptions<CapOptions> capOptions,
    IOptions<RabbitMQOptions> rabbitOptions,
    IOptions<RabbitIntegrationEventsOptions> eventOptions,
    IEventTypeProvider eventTypeProvider,
    IExternalSystemBindingsResolver bindingsResolver)
    : IRabbitSchemaExportSettings
{
    public string ExchangeName => rabbitOptions.Value.ExchangeName;

    public string FromQueueName => capOptions.Value.DefaultGroupName;

    public string System => eventOptions.Value.MessageQueue.SchemaExportSettings?.System ?? Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    public IReadOnlyDictionary<string, Type> InputEvents => eventTypeProvider.InputEvents.ToDictionary(x => x.Value, x => x.Key);

    public IReadOnlyDictionary<string, Type> OutputEvents => bindingsResolver.ResolveOutputEventsForExport();
}
