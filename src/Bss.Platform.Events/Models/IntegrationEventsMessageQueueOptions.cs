namespace Bss.Platform.Events.Models;

public class IntegrationEventsMessageQueueOptions
{
    public bool Enable { get; set; }

    // TODO: remove
    public bool EnableSchemaExport => this.SchemaExportSettings != null;

    public string Host { get; set; } = default!;

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = default!;

    public string Secret { get; set; } = default!;

    public string VirtualHost { get; set; } = default!;

    public string ExchangeName { get; set; } = default!;

    public string QueueName { get; set; } = default!;

    /// <summary>
    /// RabbitMQ queue used by schema export initializer
    /// to publish a one-time event schema payload during startup,
    /// if null, then that mechanism is disabled
    /// </summary>
    public SchemaExportSettings? SchemaExportSettings { get; set; } = new();

    /// <summary>
    /// Provide a path to section satisfied <see cref="ExternalSystemBindingsOptions"/> or configure ExternalSystemBindingsOptions by yourself, <br/>
    /// but the mapping dictionary has caveats (null and empty values skipped by default, using this parameter, you will avoid that)
    /// </summary>
    /// <example>
    /// Expected configuration:
    /// <code>
    /// {
    ///   "ExternalSystemBindings": {
    ///     "ExcludeOutputEvents": ["EXT.Debug*", "EXT.Internal.SomeEvent"],
    ///     "SystemBindings": {
    ///       "system1-allEvents-queue-except-excluded": null,
    ///       "system2-allEvents-queue-except-excluded": [],
    ///       "system3-fixedEvents-queue-exclude-not-applied": ["EXT.OrderCreated", "EXT.OrderCancelled", "EXT.Debug.Some"]
    ///     }
    ///   }
    /// }
    /// </code>
    /// </example>

    public string? ExternalSystemBindingsSectionPath { get; set; }
}
