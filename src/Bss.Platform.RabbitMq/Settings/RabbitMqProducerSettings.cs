namespace Bss.Platform.RabbitMq.Settings;

public class RabbitMqProducerSettings
{
    public TimeSpan PublishConfirmationTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Null: no system selection. Empty dictionary: explicit selection of no systems.</summary>
    public Dictionary<string, string[]?>? SystemBindings { get; set; }

    /// <summary>Excluded only from the default bindings; explicit routing lists take precedence.</summary>
    public string[] DefaultExcludeOutputBindings { get; set; } = [];
}
