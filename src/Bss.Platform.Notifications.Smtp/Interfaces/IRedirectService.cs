using System.Net.Mail;

namespace Bss.Platform.Notifications.Smtp.Interfaces;

public interface IRedirectService
{
    void Redirect(MailMessage message);
}
