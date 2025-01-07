namespace Bss.Platform.Kubernetes;

internal static class Constants
{
    public static string SqlHealthCheck => "SQLCheck";

    public static string LiveHealthCheck => "/health/live";

    public static string ReadyHealthCheck => "/health/ready";
}
