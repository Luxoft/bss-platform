using System.Net.Mail;

using Bss.Platform.Notifications.Interfaces;
using Bss.Platform.Notifications.Models;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Notifications.Services;

internal class FileSender(IOptions<NotificationSenderOptions> settings) : IMailMessageSender
{
    public async Task SendAsync(MailMessage message, CancellationToken token)
    {
        using var client = this.GetSmtpClient();
        await client.SendMailAsync(message, token);
    }

    private SmtpClient GetSmtpClient() =>
        new()
        {
            UseDefaultCredentials = true,
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = settings.Value.OutputFolder
        };
}
