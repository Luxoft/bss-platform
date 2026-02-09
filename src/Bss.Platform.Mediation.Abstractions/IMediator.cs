namespace Bss.Platform.Mediation.Abstractions;

public interface IMediator
{
    Task<TResult> Send<TRequest, TResult>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResult>;
}
