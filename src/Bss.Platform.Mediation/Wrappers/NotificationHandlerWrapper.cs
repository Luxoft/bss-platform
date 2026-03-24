namespace Bss.Platform.Mediation.Wrappers;

internal abstract class NotificationHandlerWrapper
{
    public abstract Task Handle(object notification, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
