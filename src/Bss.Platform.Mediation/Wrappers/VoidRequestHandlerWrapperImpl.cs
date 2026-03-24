using Bss.Platform.Mediation.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.Mediation.Wrappers;

internal class VoidRequestHandlerWrapperImpl<TRequest> : VoidRequestHandlerWrapper
    where TRequest : IRequest
{
    public override Task Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest>>();

        RequestHandlerDelegate handlerDelegate = () => handler.Handle((TRequest)request, cancellationToken);

        foreach (var behavior in behaviors.Reverse())
        {
            var next = handlerDelegate;
            handlerDelegate = () => behavior.Handle((TRequest)request, next, cancellationToken);
        }

        return handlerDelegate();
    }
}
