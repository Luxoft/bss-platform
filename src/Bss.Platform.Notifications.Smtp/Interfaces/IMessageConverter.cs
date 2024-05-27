using System.Net.Mail;

using Bss.Platform.Notifications.Smtp.Models;

namespace Bss.Platform.Notifications.Smtp.Interfaces;

public interface IMessageConverter
{
    MailMessage Convert(EmailDto emailDto);
}
