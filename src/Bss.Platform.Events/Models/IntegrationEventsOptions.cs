using DotNetCore.CAP;

using Microsoft.AspNetCore.Http;

namespace Bss.Platform.Events.Models;

public class IntegrationEventsOptions
{
    public string DashboardPath { get; set; } = "/admin/events";

    public string GatewayPrefix { get; set; } = string.Empty;

    public int FailedRetryCount { get; set; } = 5;

    public int RetentionDays { get; set; } = 15;

    /// <summary>
    /// required to fill connection string in options, otherwise MS SQL db won't connect
    /// </summary>
    public IntegrationEventsSqlServerOptions SqlServer { get; set; } = new() { Schema = "events" };

    public IntegrationEventsMessageQueueOptions MessageQueue { get; set; } = new() { Enable = true };

    public Action<CapOptions>? OverrideCapOptions { get; set; }

    /// <summary>
    /// When enable will be registered CAP filter to handle failed event on the last attempt, allow using multiple
    /// <see cref="Bss.Platform.Events.Abstractions.IFailedEventProcessor" /> scoped implementations
    /// </summary>
    public bool UseFailedEventProcessor { get; set; }

    /// <summary>
    /// Any condition to check that a user should get access to events dashboard
    /// </summary>
    /// <example>
    /// AuthorizationPolicyPredicate = (httpContext) => httpContext.RequestServices.GetRequiredService&lt;ICurrentUser&gt;().IsAdminAsync()
    /// </example>
    public Func<HttpContext, Task<bool>>? AuthorizationPredicate { get; set; }
}
