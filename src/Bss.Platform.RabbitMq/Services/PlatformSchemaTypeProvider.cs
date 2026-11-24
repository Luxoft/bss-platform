using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Schemas;

namespace Bss.Platform.RabbitMq.Services;

public class PlatformSchemaTypeProvider(
    IOutputEventCatalog outputs,
    IOutputBindingsResolver bindings,
    IRabbitInputEventProvider? inputs = null) : IRabbitSchemaTypeProvider
{
    public IReadOnlyDictionary<string, Type> InputEvents => inputs?.Events ?? new Dictionary<string, Type>();
    public IReadOnlyDictionary<string, Type> OutputEvents => bindings.ResolveSchemaEvents();
    public bool HasContracts => outputs.Events.Count > 0 || this.InputEvents.Count > 0;
}
