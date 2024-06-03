namespace Bss.Platform.Notifications.Models;

public class NotificationSenderOptions
{
    public const string SectionName = "NotificationSender";

    public bool IsSmtpEnabled { get; set; } = true;

    public string? OutputFolder { get; set; }

    public string Server { get; set; } = default!;

    public int Port { get; set; } = 25;

    public string[]? RedirectTo { get; set; }

    public string[]? DefaultRecipients { get; set; }
}
