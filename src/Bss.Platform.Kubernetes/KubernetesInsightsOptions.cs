using System.Reflection;

using Microsoft.Extensions.Logging;

namespace Bss.Platform.Kubernetes;

public record KubernetesInsightsOptions
{
    public bool SkipSuccessfulDependency { get; set; } = false;

    /// <summary>
    /// Skip provided by this package health check probes ("/health/live", "/health/ready")
    /// </summary>
    public bool SkipDefaultHealthChecks { get; set; } = true;

    /// <summary>
    /// Register case-insensitive http path to skip from telemetry
    /// <example> ["/DoHealthCheck"]</example>
    /// </summary>
    public string[] AdditionalHealthCheckPathToSkip { get; set; } = [];

    /// <summary>
    /// Fill AuthenticatedUserId (from HttpContext.User.Identity.Name)
    /// </summary>
    public bool SetAuthenticatedUserFromHttpContext { get; set; } = true;

    /// <summary>
    /// Used to set role name on AppInsight, by default it is assembly name
    /// </summary>
    public string RoleName { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

    /// <summary>
    /// Enable sending logs to appInsight as traces (linked to relative requests) 
    /// </summary>
    public KubernetesInsightsOptions AddLogMessages(LogLevel level = LogLevel.Information)
    {
        this.LogFilterRules.Add(new(null,level,null));
        return this;
    }
    
    /// <summary>
    /// Enable sending logs to appInsight as traces (linked to relative requests) that satisfies passed conditions (can be used multiple times)
    /// </summary>
    /// <param name="category">Filter by full class name passed as generic type for ILogger&lt;Type&gt;</param>
    /// <param name="level">Filter by log level high or equals passed</param>
    /// <param name="filter">Additional func to filter if previous condition passed</param>
    public KubernetesInsightsOptions AddLogMessages(string? category, LogLevel? level = null, Func<string?, LogLevel?, bool>? filter = null)
    {
        this.LogFilterRules.Add(new(category,level,filter));
        return this;
    }

    internal List<LogFilterRule> LogFilterRules { get; } = [];

    internal record LogFilterRule(string? Category, LogLevel? Level, Func<string?, LogLevel?, bool>? Filter);
}
