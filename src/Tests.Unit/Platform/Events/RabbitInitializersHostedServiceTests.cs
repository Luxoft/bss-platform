using Bss.Platform.Events.Interfaces;
using Bss.Platform.Events.Internal;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Tests.Unit.Platform.Events;

public class RabbitInitializersHostedServiceTests
{
    private sealed class ThrowingInitializer : IRabbitInitializer
    {
        public bool WasCalled { get; private set; }

        public Task InitializeAsync(RabbitMQ.Client.IModel model, CancellationToken cancellationToken)
        {
            this.WasCalled = true;
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class RecordingInitializer : IRabbitInitializer
    {
        public bool WasCalled { get; private set; }

        public Task InitializeAsync(RabbitMQ.Client.IModel model, CancellationToken cancellationToken)
        {
            this.WasCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RunInitializersAsync_continues_after_one_initializer_throws()
    {
        var throwing = new ThrowingInitializer();
        var recordingBefore = new RecordingInitializer();
        var recordingAfter = new RecordingInitializer();

        var service = new RabbitInitializersHostedService(
            connectionChannelPool: null!,
            initializers: [recordingBefore, throwing, recordingAfter],
            logger: NullLogger<RabbitInitializersHostedService>.Instance);

        await service.RunInitializersAsync(null!, CancellationToken.None);

        recordingBefore.WasCalled.Should().BeTrue();
        throwing.WasCalled.Should().BeTrue();
        recordingAfter.WasCalled.Should().BeTrue();
    }
}
