namespace Bss.Platform.Events.Interfaces;

public interface IEventTypeProvider
{
    IReadOnlyDictionary<Type, string> InternalEvents { get; }

    IReadOnlyDictionary<Type, string> ExternalEvents { get; }
}
