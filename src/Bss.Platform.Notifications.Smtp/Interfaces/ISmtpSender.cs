using System.Net.Mail;

namespace Bss.Platform.Notifications.Smtp.Interfaces;

public interface ISmtpSender
{
    Task SendAsync(MailMessage message);
}
