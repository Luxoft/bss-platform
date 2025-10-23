using System.Net.Mail;

namespace Bss.Platform.Notifications.Models;

public record EmailModel(
    string Subject,
    string Body,
    MailAddress From,
    MailAddress[] To,
    MailAddress[]? Cc = null,
    MailAddress[]? Bcc = null,
    MailAddress[]? ReplyTo = null,
    Attachment[]? Attachments = null);
