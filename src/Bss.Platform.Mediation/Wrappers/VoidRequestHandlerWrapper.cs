namespace Bss.Platform.Mediation.Wrappers;

internal abstract class VoidRequestHandlerWrapper
{
    public abstract Task Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
