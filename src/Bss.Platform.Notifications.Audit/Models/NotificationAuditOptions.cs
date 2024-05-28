namespace Bss.Platform.Notifications.Audit.Models;

public class NotificationAuditOptions
{
    public const string TableName = "SentMessages";

    public string Schema { get; set; } = "notifications";

    public string ConnectionString { get; set; } = default!;
}
