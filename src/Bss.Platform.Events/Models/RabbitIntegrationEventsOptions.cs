namespace Bss.Platform.Events.Models;

public class RabbitIntegrationEventsOptions : IntegrationEventsOptions
{
    /// <summary>
    /// When enable will be registered CAP filter to handle failed event on the last attempt + deadlettering
    /// Allow registering multiple scoped implementations of
    /// <c>IFailedEventProcessor&lt;TInputEvent&gt;</c>
    /// </summary>
    public bool UseFailedEventProcessor { get; set; } = true;

    /// <summary>
    /// set exchange and queue names for deadlettering (routing key for deadletter message always empty) <br/>
    /// used only when <see cref="UseFailedEventProcessor" /> is true
    /// </summary>
    public (string ExchangeName, string QueueName) DeadLetterOptions { get; set; } = ("deadletters", "deadletters");
}
