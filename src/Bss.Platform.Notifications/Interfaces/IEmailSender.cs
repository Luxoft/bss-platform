using System.Net.Mail;

using Bss.Platform.Notifications.Models;

namespace Bss.Platform.Notifications.Interfaces;

public interface IEmailSender
{
    Task<MailMessage> SendAsync(EmailModel model, CancellationToken token);
}
