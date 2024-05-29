namespace Bss.Platform.Notifications.Audit.Models;

public class NotificationAuditOptions
{
    public string Schema { get; set; } = "notifications";

    public string Table { get; set; } = "SentMessages";

    public string ConnectionString { get; set; } = default!;
}
