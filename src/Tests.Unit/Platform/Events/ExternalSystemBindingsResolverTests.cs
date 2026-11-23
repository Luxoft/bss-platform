using Bss.Platform.Events.Abstractions;
using Bss.Platform.Events.Internal;
using Bss.Platform.Events.Models;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Xunit;

namespace Tests.Unit.Platform.Events;

public class ExternalSystemBindingsResolverTests
{
    private sealed class StaticOptionsSnapshot<TOptions>(TOptions value) : IOptionsSnapshot<TOptions>
        where TOptions : class
    {
        public TOptions Value { get; } = value;

        public TOptions Get(string? name) => this.Value;
    }

    private sealed record PublicOutputEvent(string Id);

    private sealed record InternalOutputEvent(string Id);

    private sealed class FakeEventTypeProvider(
        IReadOnlyDictionary<Type, string> inputEvents,
        IReadOnlyDictionary<Type, string> outputEvents) : IEventTypeProvider
    {
        public IReadOnlyDictionary<Type, string> InputEvents { get; } = inputEvents;

        public IReadOnlyDictionary<Type, string> OutputEvents { get; } = outputEvents;
    }

    [Fact]
    public void ResolveOutputEventsForExport_applies_exclude_patterns()
    {
        var provider = new FakeEventTypeProvider(
            new Dictionary<Type, string>(),
            new Dictionary<Type, string>
            {
                [typeof(PublicOutputEvent)] = "EXT.Public.A",
                [typeof(InternalOutputEvent)] = "EXT.Internal.B"
            });

        var options = new ExternalSystemBindingsOptions
        {
            ExcludeOutputEvents = ["EXT.Internal*"],
            SystemBindings = { ["public-queue"] = null }
        };
        var resolver = new ExternalSystemBindingsResolver(
            provider,
            new StaticOptionsSnapshot<ExternalSystemBindingsOptions>(options));

        var result = resolver.ResolveOutputEventsForExport();

        result.Keys.Should().BeEquivalentTo("EXT.Public.A");
    }
}
