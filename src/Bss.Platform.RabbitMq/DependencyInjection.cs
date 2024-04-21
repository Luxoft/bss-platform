using Bss.Platform.RabbitMq.Interfaces;
using Bss.Platform.RabbitMq.Services;
using Bss.Platform.RabbitMq.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bss.Platform.RabbitMq;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformRabbitMqClient(this IServiceCollection services, IConfiguration configuration) =>
        services
            .Configure<RabbitMqServerSettings>(configuration.GetSection("RabbitMQ:Server"))
            .AddSingleton<IRabbitMqClient, RabbitMqClient>();
}
