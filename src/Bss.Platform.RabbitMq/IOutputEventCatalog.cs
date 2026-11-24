namespace Bss.Platform.RabbitMq;

public interface IOutputEventCatalog
{
    IReadOnlyDictionary<string, Type> Events { get; }
}
