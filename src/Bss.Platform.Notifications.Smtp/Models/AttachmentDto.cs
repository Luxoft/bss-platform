namespace Bss.Platform.Notifications.Smtp.Models;

public record AttachmentDto(string Name, byte[] Body, bool Inline = true);
