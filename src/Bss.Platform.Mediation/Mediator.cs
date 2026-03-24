using Bss.Platform.Mediation.Abstractions;
using Bss.Platform.Mediation.Wrappers;

namespace Bss.Platform.Mediation;

public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
        var wrapper = (RequestHandlerWrapper<TResponse>)(Activator.CreateInstance(wrapperType)
                      ?? throw new InvalidOperationException($"Could not create wrapper for {requestType}"));

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    public Task Send(IRequest request, CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var wrapperType = typeof(VoidRequestHandlerWrapperImpl<>).MakeGenericType(requestType);
        var wrapper = (VoidRequestHandlerWrapper)(Activator.CreateInstance(wrapperType)
                      ?? throw new InvalidOperationException($"Could not create wrapper for {requestType}"));

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken)
        where TNotification : INotification
    {
        var notificationType = notification.GetType();
        var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(notificationType);
        var wrapper = (NotificationHandlerWrapper)(Activator.CreateInstance(wrapperType)
                      ?? throw new InvalidOperationException($"Could not create notification wrapper for {notificationType}"));

        return wrapper.Handle(notification, serviceProvider, cancellationToken);
    }
}
