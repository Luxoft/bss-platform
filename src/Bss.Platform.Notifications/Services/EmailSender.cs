using System.Net.Mail;
using System.Text.RegularExpressions;

using Bss.Platform.Notifications.Interfaces;
using Bss.Platform.Notifications.Models;

namespace Bss.Platform.Notifications.Services;

internal class EmailSender(IEnumerable<IMailMessageSender> senders, IRedirectService? redirectService = null, IAuditService? auditService = null) : IEmailSender
{
    public async Task<MailMessage> SendAsync(EmailModel model, CancellationToken token)
    {
        var message = Convert(model);

        redirectService?.Redirect(message);

        foreach (var sender in senders)
        {
            await sender.SendAsync(message, token);
        }

        if (auditService != null)
        {
            await auditService.LogAsync(message, token);
        }

        return message;
    }

    private static MailMessage Convert(EmailModel model)
    {
        var mailMessage = new MailMessage { Subject = model.Subject, Body = model.Body, From = model.From, IsBodyHtml = true };

        AddRange(mailMessage.To, model.To);

        if (model.Cc?.Length > 0)
        {
            AddRange(mailMessage.CC, model.Cc);
        }

        if (model.ReplyTo?.Length > 0)
        {
            AddRange(mailMessage.ReplyToList, model.ReplyTo);
        }

        if (model.Attachments?.Length > 0)
        {
            SetAttachments(model.Attachments, mailMessage);
        }

        return mailMessage;
    }

    private static void SetAttachments(Attachment[] attachments, MailMessage mailMessage)
    {
        foreach (var attachment in attachments)
        {
            mailMessage.Attachments.Add(attachment);
            if (!attachment.ContentDisposition!.Inline)
            {
                continue;
            }

            var srcRegex = $"src\\s*=\\s*\"{attachment.Name}\"";
            if (!Regex.IsMatch(mailMessage.Body, srcRegex, RegexOptions.IgnoreCase))
            {
                continue;
            }

            mailMessage.Body = Regex.Replace(mailMessage.Body, srcRegex, $"src=\"cid:{attachment.ContentId}\"", RegexOptions.IgnoreCase);
        }
    }

    private static void AddRange(MailAddressCollection collection, IEnumerable<MailAddress> addresses)
    {
        foreach (var address in addresses)
        {
            collection.Add(address);
        }
    }
}
