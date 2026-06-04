namespace Bss.Platform.Events.Models;

public class IntegrationEventsSqlServerOptions
{
    /// <summary>
    /// When empty - MS SQL db won't connect
    /// </summary>
    public string ConnectionString { get; set; } = default!;

    public string Schema { get; set; } = default!;
}
