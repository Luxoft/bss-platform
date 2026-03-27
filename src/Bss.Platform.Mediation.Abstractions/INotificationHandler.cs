namespace Bss.Platform.Mediation.Abstractions;

public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    public Task Handle(TNotification notification, CancellationToken cancellationToken =  default);
}
