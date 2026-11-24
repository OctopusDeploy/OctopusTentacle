namespace Octopus.Pipelines.Runner.Docker;

/// <summary>Settings for <see cref="DockerJobBackend"/>.</summary>
public sealed class DockerBackendOptions
{
    /// <summary>
    /// The Docker CLI to run, by name on the PATH or as a full path. Which daemon it talks to is the CLI's own
    /// choice: <c>DOCKER_HOST</c>, then the current context.
    /// </summary>
    public string Executable { get; set; } = "docker";

    /// <summary>The image every job runs in. It must provide <c>sh</c>.</summary>
    public string JobImage { get; set; } = "busybox:1.37";

    public long MemoryLimitBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>CPUs the job may use, e.g. 0.25 for a quarter of one.</summary>
    public double CpuLimit { get; set; } = 0.25;

    /// <summary>The <c>uid:gid</c> the job runs as.</summary>
    public string User { get; set; } = "65534:65534";

    public bool ReadOnlyRootFilesystem { get; set; } = true;
}
