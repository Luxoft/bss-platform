namespace Bss.Platform.RabbitMq.Interfaces;

public interface IOutputBindingsResolver
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveQueueBindings();
    IReadOnlyDictionary<string, Type> ResolveSchemaEvents();
}
