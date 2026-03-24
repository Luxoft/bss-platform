namespace Bss.Platform.Mediation.Abstractions;

public interface IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);

    public Task Send(IRequest request, CancellationToken cancellationToken);

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken)
        where TNotification : INotification;
}
