using Bss.Platform.Notifications.Audit.Models;
using Bss.Platform.Notifications.Audit.Services;
using Bss.Platform.Notifications.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bss.Platform.Notifications.Audit;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformNotificationsAudit(
        this IServiceCollection services,
        Action<NotificationAuditOptions>? setup = null)
    {
        var settings = new NotificationAuditOptions();
        setup?.Invoke(settings);

        return services.AddHostedService<AuditSchemaMigrationService>()
                       .AddScoped<IAuditService, AuditService>()
                       .AddSingleton<IOptions<NotificationAuditOptions>>(_ => Options.Create(settings));
    }
}
