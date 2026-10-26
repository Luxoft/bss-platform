namespace Bss.Platform.Events.Abstractions;

public interface IFailedEventProcessor<in TInputEvent>
{
    Task HandleAsync(TInputEvent? value, Exception ex, string? rawMessageBody);
}
