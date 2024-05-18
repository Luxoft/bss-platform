namespace Bss.Platform.Events.Models;

public class IntegrationEventsOptions
{
    public string DashboardPath { get; set; } = default!;

    public int FailedRetryCount { get; set; }

    public int RetentionDays { get; set; }

    public IntegrationEventsSqlServerOptions SqlServer { get; set; } = default!;

    public IntegrationEventsMessageQueueOptions MessageQueue { get; set; } = default!;

    public static IntegrationEventsOptions Default =>
        new()
        {
            DashboardPath = "/admin/events",
            FailedRetryCount = 5,
            RetentionDays = 15,
            SqlServer = new IntegrationEventsSqlServerOptions { Schema = "events" },
            MessageQueue = new IntegrationEventsMessageQueueOptions { Enable = true }
        };
}
