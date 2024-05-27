using System.Net.Mail;

namespace Bss.Platform.Notifications.Smtp.Interfaces;

public interface ISentMessageService
{
    /// <summary>
    /// Post-process sent message. It is generally recommended to log sent messages to DB
    /// </summary>
    Task ProcessAsync(MailMessage message, CancellationToken token);
}
