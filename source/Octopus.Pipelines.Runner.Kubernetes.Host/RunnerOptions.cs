namespace Octopus.Pipelines.Runner.Kubernetes.Host;

/// <summary>
/// The <c>Runner</c> configuration section. The gateway binds it as <see cref="Gateway.GatewayOptions"/> (server address,
/// capacity), and the Kubernetes backend binds its <c>Kubernetes</c> subsection.
/// </summary>
public static class RunnerOptions
{
    public const string Section = "Runner";
}
