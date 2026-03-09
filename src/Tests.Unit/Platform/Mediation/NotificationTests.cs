using Bss.Platform.Mediation;
using Bss.Platform.Mediation.Abstractions;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Tests.Unit.Platform.Mediation;

public class NotificationTests
{
    private readonly ServiceCollection services = [];

    private readonly List<string> executionLog = [];

    public NotificationTests()
    {
        this.services.AddSingleton(this.executionLog);
        this.services.AddTransient<IMediator, Mediator>();
    }

    public record AlertNotification : INotification;

    public class AlertHandler1(List<string> log) : INotificationHandler<AlertNotification>
    {
        public Task Handle(AlertNotification notification, CancellationToken cancellationToken)
        {
            log.Add("Handler1");
            return Task.CompletedTask;
        }
    }

    public class AlertHandler2(List<string> log) : INotificationHandler<AlertNotification>
    {
        public Task Handle(AlertNotification notification, CancellationToken cancellationToken)
        {
            log.Add("Handler2");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Publish_Notification_ExecutesAllHandlers()
    {
        // Arrange
        this.services.AddTransient<INotificationHandler<AlertNotification>, AlertHandler1>();
        this.services.AddTransient<INotificationHandler<AlertNotification>, AlertHandler2>();

        var provider = this.services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        await mediator.Publish(new AlertNotification());

        // Assert
        this.executionLog.Should().Contain("Handler1");
        this.executionLog.Should().Contain("Handler2");
        this.executionLog.Should().HaveCount(2);
    }
}

