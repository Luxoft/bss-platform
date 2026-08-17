using Bss.Platform.Events.Models;

namespace Bss.Platform.Events.Internal;

internal interface IExternalSystemBindingsResolver
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveQueueBindings();

    IReadOnlyDictionary<string, Type> ResolveOutputEventsForExport();
}
