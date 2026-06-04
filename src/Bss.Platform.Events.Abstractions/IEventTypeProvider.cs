namespace Bss.Platform.Events.Abstractions;

public interface IEventTypeProvider
{
    /// <summary>
    /// Internal events are sent to the Rabbit->CAP queue and handled within the target system
    /// </summary>
    IReadOnlyDictionary<Type, string> InputEvents { get; }

    /// <summary>
    /// External events are sent to the Rabbit exchange, but do not have a handler in the target system
    /// </summary>
    IReadOnlyDictionary<Type, string> OutputEvents { get; }
}
