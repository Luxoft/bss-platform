using Bss.Platform.Notifications.Smtp.Models;

namespace Bss.Platform.Notifications.Smtp.Interfaces;

public interface INotificationSender
{
    Task SendAsync(EmailDto emailDto, CancellationToken token);
}
