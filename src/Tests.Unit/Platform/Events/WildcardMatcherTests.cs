using Bss.Platform.Events.Internal;

using FluentAssertions;

using Xunit;

namespace Tests.Unit.Platform.Events;

public class WildcardMatcherTests
{
    [Theory]
    [InlineData("EXT.OrderCreated", "EXT.OrderCreated", true)]
    [InlineData("EXT.OrderCreated", "EXT.OrderCancelled", false)]
    [InlineData("EXT.Debug.Ping", "EXT.Debug*", true)]
    [InlineData("EXT.Something", "EXT.Debug*", false)]
    [InlineData("EXT.Internal.Debug", "*.Debug", true)]
    [InlineData("EXT.Internal.Debugger", "*.Debug", false)]
    [InlineData("EXT.Internal.Debug.Extra", "EXT.*.Debug.*", true)]
    [InlineData("anything", "*", true)]
    [InlineData("EXT.OrderCreated", "ext.ordercreated", true)]
    [InlineData("EXT.DEBUG.Ping", "ext.debug*", true)]
    public void IsMatch_returns_expected_result(string value, string pattern, bool expected) =>
        WildcardMatcher.IsMatch(value, pattern).Should().Be(expected);
}
