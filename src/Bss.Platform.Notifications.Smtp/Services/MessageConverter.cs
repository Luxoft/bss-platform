using System.Net.Mail;
using System.Text.RegularExpressions;

using Bss.Platform.Notifications.Smtp.Interfaces;
using Bss.Platform.Notifications.Smtp.Models;

namespace Bss.Platform.Notifications.Smtp.Services;

internal class MessageConverter : IMessageConverter
{
    public MailMessage Convert(EmailDto emailDto)
    {
        var mailMessage = new MailMessage
                          {
                              Subject = emailDto.Subject, Body = emailDto.Body, From = new MailAddress(emailDto.From), IsBodyHtml = true
                          };

        mailMessage.To.AddRange(emailDto.To.Select(x => new MailAddress(x)));

        if (emailDto.Cc?.Length > 0)
        {
            mailMessage.CC.AddRange(emailDto.Cc.Select(x => new MailAddress(x)));
        }

        if (emailDto.ReplyTo?.Length > 0)
        {
            mailMessage.CC.AddRange(emailDto.ReplyTo.Select(x => new MailAddress(x)));
        }

        if (emailDto.Attachments?.Length > 0)
        {
            SetAttachments(emailDto.Attachments, mailMessage);
        }

        return mailMessage;
    }

    private static void SetAttachments(AttachmentDto[] attachments, MailMessage mailMessage)
    {
        foreach (var attachment in attachments)
        {
            var mailAttachment = GetMailAttachment(attachment);
            mailMessage.Attachments.Add(mailAttachment);

            if (!attachment.Inline)
            {
                continue;
            }

            var srcRegex = $"src\\s*=\\s*\"{attachment.Name}\"";
            if (!Regex.IsMatch(mailMessage.Body, srcRegex, RegexOptions.IgnoreCase))
            {
                continue;
            }

            mailMessage.Body = Regex.Replace(
                mailMessage.Body,
                srcRegex,
                $"src=\"cid:{mailAttachment.ContentId}\"",
                RegexOptions.IgnoreCase);
        }
    }

    private static Attachment GetMailAttachment(AttachmentDto attachmentDto)
    {
        var stream = new MemoryStream(attachmentDto.Body);

        var attachment = new Attachment(stream, attachmentDto.Name);

        attachment.ContentDisposition!.Inline = attachmentDto.Inline;

        return attachment;
    }
}
