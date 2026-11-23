namespace Bss.Platform.Events.Models;

public class ExternalSystemBindingsOptions
{
    /// <summary>
    /// Key is a queue name to declare, value is an explicit list of output-event routing keys to bind it to.<br/>
    /// <c>null</c> or an empty array means the default set: all <see cref="Abstractions.IEventTypeProvider.OutputEvents" />
    /// except those matching <see cref="ExcludeOutputEvents" />.
    /// </summary>
    public Dictionary<string, string[]?> SystemBindings { get; set; } = new();

    /// <summary>
    /// Exact routing keys or <c>*</c>-masks excluded from the default set.<br/>
    /// Applied only when a queue in <see cref="SystemBindings" /> has no explicit routing keys, to filter from all output events.
    /// </summary>
    public string[] ExcludeOutputEvents { get; set; } = [];
}
