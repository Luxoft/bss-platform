namespace Bss.Platform.Events.Models;

public class IntegrationEventsSqlServerOptions
{
    public string ConnectionString { get; set; } = default!;

    public string Schema { get; set; } = default!;
}
