namespace Bss.Platform.Events.Abstractions;

public interface IFailedEventProcessor
{
    Task HandleAsync(object? value, Exception ex);
}
