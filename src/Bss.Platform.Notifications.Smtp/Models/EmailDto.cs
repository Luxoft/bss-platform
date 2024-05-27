namespace Bss.Platform.Notifications.Smtp.Models;

public record EmailDto(string Subject, string Body, string From, string[] To)
{
    public string[]? Cc { get; set; }

    public string[]? ReplyTo { get; set; }

    public AttachmentDto[]? Attachments { get; set; }
}
