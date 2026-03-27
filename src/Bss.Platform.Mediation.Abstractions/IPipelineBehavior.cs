namespace Bss.Platform.Mediation.Abstractions;

public interface IPipelineBehavior<in TRequest, TResult>
{
    public Task<TResult> Handle(TRequest request, RequestHandlerDelegate<TResult> next, CancellationToken cancellationToken = default);
}

public interface IPipelineBehavior<in TRequest>
{
    public Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken = default);
}
