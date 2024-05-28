using System.Net.Mail;

namespace Bss.Platform.Notifications.Interfaces;

public interface IMailMessageSender
{
    Task SendAsync(MailMessage message, CancellationToken token);
}
