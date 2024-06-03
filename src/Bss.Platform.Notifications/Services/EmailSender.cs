using System.Net.Mail;
using System.Text.RegularExpressions;

using Bss.Platform.Notifications.Interfaces;
using Bss.Platform.Notifications.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bss.Platform.Notifications.Services;

internal class EmailSender(
    IEnumerable<IMailMessageSender> senders,
    IOptions<NotificationSenderOptions> settings,
    ILogger<EmailSender> logger,
    IAuditService? auditService = null) : IEmailSender
{
    protected NotificationSenderOptions Settings => settings.Value;

    public async Task<MailMessage> SendAsync(EmailModel emailModel, CancellationToken token)
    {
        var message = this.Convert(emailModel);

        foreach (var sender in senders)
        {
            await sender.SendAsync(message, token);
        }

        if (auditService is not null)
        {
            await auditService.LogAsync(message, token);
        }

        return message;
    }

    protected virtual MailMessage Convert(EmailModel model)
    {
        var mailMessage = new MailMessage { Subject = model.Subject, Body = model.Body, From = model.From, IsBodyHtml = true };

        if (model.To.Length != 0)
        {
            AddRange(mailMessage.To, model.To);
        }
        else
        {
            AddRange(mailMessage.To, settings.Value.DefaultRecipients.Select(x => new MailAddress(x)));

            logger.LogWarning(
                "Recipients are not provided for email '{subject}', redirecting to '{default}'",
                mailMessage.Subject,
                mailMessage.To);
        }

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
