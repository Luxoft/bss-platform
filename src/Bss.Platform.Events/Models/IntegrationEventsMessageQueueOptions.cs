namespace Bss.Platform.Events.Models;

public class IntegrationEventsMessageQueueOptions
{
    public bool Enable { get; set; }

    public string Host { get; set; } = default!;

    public int Port { get; set; }

    public string UserName { get; set; } = default!;

    public string Secret { get; set; } = default!;

    public string VirtualHost { get; set; } = default!;

    public string ExchangeName { get; set; } = default!;
}
