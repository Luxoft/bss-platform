namespace Bss.Platform.RabbitMq.Schemas;

public interface IRabbitSchemaTypeProvider
{
    IReadOnlyDictionary<string, Type> InputEvents { get; }
    IReadOnlyDictionary<string, Type> OutputEvents { get; }
    bool HasContracts => this.InputEvents.Count > 0 || this.OutputEvents.Count > 0;
}
