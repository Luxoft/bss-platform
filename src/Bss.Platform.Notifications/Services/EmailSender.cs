using System.Net.Mail;
using System.Text.RegularExpressions;

using Bss.Platform.Notifications.Interfaces;
using Bss.Platform.Notifications.Models;

namespace Bss.Platform.Notifications.Services;

internal class EmailSender(IEnumerable<IMailMessageSender> senders, IAuditService? auditService = null) : IEmailSender
{
    public async Task<MailMessage> SendAsync(EmailModel emailModel, CancellationToken token)
    {
        var message = this.Convert(emailModel);

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

    protected virtual MailMessage Convert(EmailModel model)
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
            if (!attachment.ContentDisposition!.Inline)
            {
                mailMessage.Attachments.Add(attachment);
                continue;
            }

            var srcRegex = $"src\\s*=\\s*\"{attachment.Name}\"";
            if (!Regex.IsMatch(mailMessage.Body, srcRegex, RegexOptions.IgnoreCase))
            {
                continue;
            }

            mailMessage.Body = Regex.Replace(mailMessage.Body, srcRegex, $"src=\"cid:{attachment.ContentId}\"", RegexOptions.IgnoreCase);
            mailMessage.Attachments.Add(attachment);
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
