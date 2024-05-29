using System.Net.Mail;

using Bss.Platform.Notifications.Interfaces;
using Bss.Platform.Notifications.Models;

namespace Bss.Platform.Notifications.Services;

internal class TestEmailSender(IEnumerable<IMailMessageSender> senders, IRedirectService redirectService, IAuditService? auditService = null)
    : EmailSender(senders, auditService)
{
    protected override MailMessage Convert(EmailModel model)
    {
        var message = base.Convert(model);
        redirectService.Redirect(message);
        return message;
    }
}
