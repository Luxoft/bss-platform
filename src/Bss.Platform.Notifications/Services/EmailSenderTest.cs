using System.Net.Mail;

using Bss.Platform.Notifications.Interfaces;
using Bss.Platform.Notifications.Models;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Notifications.Services;

internal class EmailSenderTest(
    IEnumerable<IMailMessageSender> senders,
    IOptions<NotificationSenderOptions> settings,
    IAuditService? auditService = null)
    : EmailSender(senders, auditService)
{
    protected override MailMessage Convert(EmailModel model)
    {
        var message = base.Convert(model);
        this.ChangeRecipients(message);

        return message;
    }

    private void ChangeRecipients(MailMessage message)
    {
        AddRecipientsToBody(message);

        ClearRecipients(message);

        foreach (var address in settings.Value.RedirectTo!.Select(z => z.Trim()).Distinct().Select(z => new MailAddress(z)))
        {
            message.To.Add(address);
        }
    }

    private static void AddRecipientsToBody(MailMessage message)
    {
        var originalRecipients =
            $"From: {message.From!.Address}<br>"
            + $"To: {string.Join("; ", message.To.Select(x => x.Address))}<br>"
            + $"CC: {string.Join("; ", message.CC.Select(x => x.Address))}<br>"
            + $"Reply To: {string.Join("; ", message.ReplyToList.Select(x => x.Address))}<br><br>";

        message.Body = $"{originalRecipients}{message.Body}";
    }

    private static void ClearRecipients(MailMessage message)
    {
        message.To.Clear();
        message.CC.Clear();
        message.Bcc.Clear();
        message.ReplyToList.Clear();
    }
}
