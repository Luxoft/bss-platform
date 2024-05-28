using System.Net.Mail;

namespace Bss.Platform.Notifications.Interfaces;

public interface IRedirectService
{
    void Redirect(MailMessage message);
}
