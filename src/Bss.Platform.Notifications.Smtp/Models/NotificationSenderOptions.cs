namespace Bss.Platform.Notifications.Smtp.Models;

public class NotificationSenderOptions
{
    public const string SectionName = "NotificationSender";

    public bool SmtpEnabled { get; set; } = true;

    public string? OutputFolder { get; set; }

    public string Server { get; set; } = default!;

    public int Port { get; set; } = default!;

    public string[]? RedirectTo { get; set; }
}
