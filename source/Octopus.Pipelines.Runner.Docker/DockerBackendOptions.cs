namespace Octopus.Pipelines.Runner.Docker;

/// <summary>Settings for <see cref="DockerJobBackend"/>.</summary>
public sealed class DockerBackendOptions
{
    /// <summary>
    /// The Docker CLI to run, by name on the PATH or as a full path. Which daemon it talks to is the CLI's own
    /// choice: <c>DOCKER_HOST</c>, then the current context.
    /// </summary>
    public string Executable { get; set; } = "docker";

    /// <summary>The image every job runs in, and the image of steps that don't name one. It must provide <c>sh</c>.</summary>
    public string JobImage { get; set; } = "busybox:1.37";

    public long MemoryLimitBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>CPUs the job may use, e.g. 0.25 for a quarter of one.</summary>
    public double CpuLimit { get; set; } = 0.25;

    /// <summary>The <c>uid:gid</c> the job runs as.</summary>
    public string User { get; set; } = "65534:65534";

    public bool ReadOnlyRootFilesystem { get; set; } = true;

    /// <summary>
    /// The statically linked step runner executable, bind mounted into every step's container as its entry point.
    /// Needed for jobs with steps.
    /// </summary>
    public string? StepRunnerPath { get; set; }

    /// <summary>
    /// Where a job with steps keeps its workspace, inputs, outputs and results, as <c>&lt;root&gt;/octopus-job-&lt;job&gt;</c>,
    /// bind mounted into each step's container at <c>/octopus</c>. The daemon must see the same path, so a remote
    /// daemon won't do. Defaults to the system temp directory.
    /// </summary>
    public string? StateRoot { get; set; }

    /// <summary>Leaves the job's state directory behind when it finishes, for looking at what the steps wrote.</summary>
    public bool KeepState { get; set; }
}
