namespace Bss.Platform.Mediation.Abstractions;

public interface IPipelineBehavior<TRequest, TResult>
{
    Task<TResult> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResult>> next);
}

public interface IPipelineBehavior<TRequest>
{
    Task Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task> next);
}
