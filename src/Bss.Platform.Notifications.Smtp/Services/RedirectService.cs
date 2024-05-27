using System.Net.Mail;

using Bss.Platform.Notifications.Smtp.Interfaces;
using Bss.Platform.Notifications.Smtp.Models;

using Microsoft.Extensions.Options;

namespace Bss.Platform.Notifications.Smtp.Services;

internal record RedirectService : IRedirectService
{
    private readonly NotificationSenderOptions settings;

    public RedirectService(IOptions<NotificationSenderOptions> settings) => this.settings = settings.Value;

    public void Redirect(MailMessage message)
    {
        AddRecipientsToBody(message);

        ClearRecipients(message);

        message.To.AddRange(this.settings.RedirectTo!.Select(z => z.Trim()).Distinct().Select(z => new MailAddress(z)));
    }

    private static void ClearRecipients(MailMessage message)
    {
        message.To.Clear();
        message.CC.Clear();
        message.Bcc.Clear();
        message.ReplyToList.Clear();
    }

    private static void AddRecipientsToBody(MailMessage message)
    {
        var originalRecipients =
            $"From: {message.From!.Address}<br>"
            + $"To: {message.To.Select(x => x.Address).Join("; ")}<br>"
            + $"CC: {message.CC.Select(x => x.Address).Join("; ")}<br>"
            + $"Reply To: {message.ReplyToList.Select(x => x.Address).Join("; ")}<br><br>";

        message.Body = $"{originalRecipients}{message.Body}";
    }
}
