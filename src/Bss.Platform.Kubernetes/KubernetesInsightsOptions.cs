using System.Reflection;

namespace Bss.Platform.Kubernetes;

public record KubernetesInsightsOptions
{
    public bool SkipSuccessfulDependency { get; set; } = false;

    /// <summary>
    /// Skip provided by this package health check probes ("/health/live", "/health/ready")
    /// </summary>
    public bool SkipDefaultHealthChecks { get; set; } = true;

    /// <summary>
    /// Register insensitive http path to skip from telemetry
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
}
