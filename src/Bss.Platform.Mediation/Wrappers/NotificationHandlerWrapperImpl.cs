using Bss.Platform.Mediation.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.Mediation.Wrappers;

internal class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    public override async Task Handle(object notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();

        foreach (var handler in handlers)
        {
            await handler.Handle((TNotification)notification, cancellationToken);
        }
    }
}
