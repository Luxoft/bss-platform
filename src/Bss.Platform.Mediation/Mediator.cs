using Microsoft.Extensions.DependencyInjection;

using Bss.Platform.Mediation.Abstractions;

namespace Bss.Platform.Mediation;

public record Mediator(IServiceProvider ServiceProvider) : IMediator
{
    public Task<TResult> Send<TRequest, TResult>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResult>
    {
        var handler = this.ServiceProvider.GetRequiredService<IRequestHandler<TRequest, TResult>>();
        var behaviors = this.ServiceProvider
                            .GetServices<IPipelineBehavior<TRequest, TResult>>()
                            .Reverse()
                            .ToArray();

        Func<TRequest, CancellationToken, Task<TResult>> next = (r, ct)
                                                                    => handler.Handle(r, ct);
        foreach (var behavior in behaviors)
        {
            var prev = next;
            next = (r, ct) => behavior.Handle(r, ct, prev);
        }

        return next(request, cancellationToken);
    }
}
