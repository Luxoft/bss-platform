using Bss.Platform.Notifications.Smtp.Interfaces;
using Bss.Platform.Notifications.Smtp.Models;

namespace Bss.Platform.Notifications.Smtp.Services;

internal record NotificationSender : INotificationSender
{
    private readonly IMessageConverter messageConverter;

    private readonly IEnumerable<ISmtpSender> senders;

    private readonly IRedirectService? recipientService;

    private readonly ISentMessageService sentMessageService;

    public NotificationSender(
        IMessageConverter messageConverter,
        IEnumerable<ISmtpSender> senders,
        ISentMessageService sentMessageService,
        IRedirectService? recipientService = null)
    {
        this.messageConverter = messageConverter;
        this.senders = senders;
        this.recipientService = recipientService;
        this.sentMessageService = sentMessageService;
    }

    public async Task SendAsync(EmailDto emailDto, CancellationToken token)
    {
        var message = this.messageConverter.Convert(emailDto);
        if (this.recipientService != null)
        {
            this.recipientService!.Redirect(message);
        }

        foreach (var sender in this.senders)
        {
            await sender.SendAsync(message);
        }

        await this.sentMessageService.ProcessAsync(message, token);
    }
}
