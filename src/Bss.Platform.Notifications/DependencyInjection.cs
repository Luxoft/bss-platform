using Bss.Platform.Notifications.Interfaces;
using Bss.Platform.Notifications.Models;
using Bss.Platform.Notifications.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bss.Platform.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformNotifications(
        this IServiceCollection services,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(NotificationSenderOptions.SectionName).Get<NotificationSenderOptions>()!;

        return services.AddEmailSender(hostEnvironment, settings)
                       .AddMailMessageSenders(settings)
                       .Configure<NotificationSenderOptions>(configuration.GetSection(NotificationSenderOptions.SectionName));
    }

    private static IServiceCollection AddEmailSender(
        this IServiceCollection services,
        IHostEnvironment hostEnvironment,
        NotificationSenderOptions settings)
    {
        if (settings.RedirectTo?.Length == 0)
        {
            throw new ArgumentException("Test email address is not provided");
        }

        if (hostEnvironment.IsProduction())
        {
            return services.AddScoped<IEmailSender, EmailSender>();
        }

        if (settings.DefaultRecipients?.Length == 0)
        {
            throw new ArgumentException("Default email address is not provided");
        }

        return services.AddScoped<IEmailSender, EmailSenderTest>();
    }

    private static IServiceCollection AddMailMessageSenders(this IServiceCollection services, NotificationSenderOptions settings)
    {
        if (settings.IsSmtpEnabled)
        {
            services.AddScoped<IMailMessageSender, SmtpSender>();
        }

        if (!string.IsNullOrWhiteSpace(settings.OutputFolder))
        {
            services.AddScoped<IMailMessageSender, FileSender>();
        }

        return services;
    }
}
