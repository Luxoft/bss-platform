namespace Bss.Platform.RabbitMq.Settings;

public class RabbitMqSettings
{
    /// <summary>Abort host startup if the initial broker connection attempts are exhausted.</summary>
    public bool RequireBrokerOnStartup { get; set; }
    public string? Exchange { get; set; }
    public int? ConnectionAttemptCount { get; set; }
}
