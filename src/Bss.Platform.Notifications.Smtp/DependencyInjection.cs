using Bss.Platform.Notifications.Smtp.Interfaces;
using Bss.Platform.Notifications.Smtp.Models;
using Bss.Platform.Notifications.Smtp.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bss.Platform.Notifications.Smtp;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformNotificationSender<TSentMessageService>(
        this IServiceCollection services,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration)
        where TSentMessageService : class, ISentMessageService
    {
        AddTestEnvironmentRedirection(services, hostEnvironment, configuration);

        services.AddOptions<NotificationSenderOptions>().Bind(configuration.GetSection(NotificationSenderOptions.SectionName));

        services.AddScoped<INotificationSender, NotificationSender>()
                .AddScoped<ISentMessageService, TSentMessageService>()
                .AddScoped<IMessageConverter, MessageConverter>();

        return services;
    }

    public static IServiceCollection AddPlatformSmtpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = BindSettings(configuration);

        if (settings.SmtpEnabled)
        {
            services.AddScoped<ISmtpSender, SmtpSender>();
        }

        if (!string.IsNullOrWhiteSpace(settings.OutputFolder))
        {
            services.AddScoped<ISmtpSender, FileSender>();
        }

        return services;
    }

    private static void AddTestEnvironmentRedirection(
        IServiceCollection services,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration)
    {
        if (hostEnvironment.IsProduction())
        {
            return;
        }

        if (BindSettings(configuration).RedirectTo?.Length <= 0)
        {
            throw new ArgumentException("Test email address is not provided");
        }

        services.AddScoped<IRedirectService, RedirectService>();
    }

    private static NotificationSenderOptions BindSettings(IConfiguration configuration)
    {
        var settings = new NotificationSenderOptions();
        configuration.GetSection(NotificationSenderOptions.SectionName).Bind(settings);

        return settings;
    }
}
