using System.Net.Mail;

namespace Bss.Platform.Notifications.Interfaces;

public interface IAuditService
{
    Task LogAsync(MailMessage message, CancellationToken token);
}
