namespace Octopus.Pipelines.Runner.Kubernetes.Host;

public enum JobBackendKind
{
    /// <summary>Runs each job as a bare pod in the cell's AKS cluster.</summary>
    Kubernetes,

    /// <summary>Runs each job as a local <c>sh -c</c> process. For local development without a cluster.</summary>
    Process,
}

/// <summary>
/// The host's half of the <c>Runner</c> section. The gateway's half (server address, capacity) is
/// <see cref="Gateway.GatewayOptions"/>, bound from the same section.
/// </summary>
public sealed class RunnerOptions
{
    public const string Section = "Runner";

    public JobBackendKind Backend { get; set; } = JobBackendKind.Kubernetes;
}
