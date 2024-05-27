using System.Net.Mail;

using Bss.Platform.Notifications.Smtp.Interfaces;
using Bss.Platform.Notifications.Smtp.Models;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Notifications.Smtp.Services;

internal record SmtpSender : ISmtpSender
{
    private readonly NotificationSenderOptions settings;

    public SmtpSender(IOptions<NotificationSenderOptions> settings) => this.settings = settings.Value;

    public Task SendAsync(MailMessage message) => this.GetSmtpClient().SendMailAsync(message);

    private SmtpClient GetSmtpClient() => new(this.settings.Server, this.settings.Port) { UseDefaultCredentials = true };
}
