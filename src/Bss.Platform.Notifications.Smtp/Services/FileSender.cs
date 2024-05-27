using System.Net.Mail;

using Bss.Platform.Notifications.Smtp.Interfaces;
using Bss.Platform.Notifications.Smtp.Models;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Notifications.Smtp.Services;

internal record FileSender : ISmtpSender
{
    private readonly NotificationSenderOptions settings;

    public FileSender(IOptions<NotificationSenderOptions> settings) => this.settings = settings.Value;

    public Task SendAsync(MailMessage message) => this.GetSmtpClient().SendMailAsync(message);

    private SmtpClient GetSmtpClient() =>
        new()
        {
            UseDefaultCredentials = true,
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = this.settings.OutputFolder
        };
}
