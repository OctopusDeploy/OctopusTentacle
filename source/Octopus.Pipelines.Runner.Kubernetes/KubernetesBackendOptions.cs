namespace Octopus.Pipelines.Runner.Kubernetes;

/// <summary>Bound from <c>Runner:Kubernetes</c>.</summary>
public sealed class KubernetesBackendOptions
{
    public string Namespace { get; set; } = "poc-jobs";

    public string JobImage { get; set; } = "busybox:1.37";

    public string CpuRequest { get; set; } = "10m";

    public string MemoryRequest { get; set; } = "16Mi";

    public string MemoryLimit { get; set; } = "32Mi";

    /// <summary>Stops one job starving its neighbours of CPU. Without it a pod can use the whole node.</summary>
    public string CpuLimit { get; set; } = "250m";

    public string EphemeralStorageRequest { get; set; } = "8Mi";

    /// <summary>Caps the container's logs and writable layer so one job can't fill the node's disk and get its neighbours evicted.</summary>
    public string EphemeralStorageLimit { get; set; } = "64Mi";

    /// <summary>
    /// Runs standard jobs in their own user namespace (<c>hostUsers: false</c>), so uid 0 inside the
    /// container is an unprivileged uid on the node. Needs Kubernetes 1.33+ on Azure Linux 3 or Ubuntu 24.04.
    /// Not applied to sandboxed jobs: those already have their own kernel.
    /// </summary>
    public bool UserNamespaces { get; set; } = true;

    public Dictionary<string, string> NodeSelector { get; set; } = [];

    public List<TolerationOptions> Tolerations { get; set; } = [];

    /// <summary>Where and how <c>Sandboxed</c> jobs run. Sandboxed jobs fail if this isn't enabled.</summary>
    public SandboxOptions Sandbox { get; set; } = new();

    /// <summary>Caps concurrent create-pod calls per runner so a big dispatch doesn't stampede the API server.</summary>
    public int MaxParallelPodCreates { get; set; } = 20;

    /// <summary>How long a pod may sit unscheduled or pulling before the job fails. Covers node autoscale-up.</summary>
    /// <remarks>
    /// Keep this at most the Server's <c>Storage:Grants:StartAllowance</c> (both default to 15 minutes): the job's SAS
    /// only allows that long to start, so a pod that starts later can lose log access before it reaches its maximum runtime.
    /// </remarks>
    public TimeSpan PodStartTimeout { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How jobs with steps run: a pod per step, on storage shared with the runner.</summary>
    public StepsOptions Steps { get; set; } = new();
}

/// <summary>
/// Jobs with steps run one pod per step. Each pod mounts the job's directory on shared storage at <c>/octopus</c>
/// (the workspace, and the step's input, output and result), and the runner reads and writes the same directory at
/// <see cref="LocalPath"/>. Set exactly one of <see cref="ClaimName"/> and <see cref="HostPath"/>.
/// </summary>
public sealed class StepsOptions
{
    /// <summary>An image whose <c>/step-runner</c> is the static step runner; an init container copies it into each step pod.</summary>
    public string StepRunnerImage { get; set; } = "octopus/pipelines-step-runner:dev";

    /// <summary>A ReadWriteMany PersistentVolumeClaim, also mounted into the runner's pod at <see cref="LocalPath"/>.</summary>
    public string? ClaimName { get; set; }

    /// <summary>
    /// A directory on the node, for single-node development clusters (kind) only: Pod Security "restricted" forbids
    /// it. The runner must see the same directory at <see cref="LocalPath"/>.
    /// </summary>
    public string? HostPath { get; set; }

    /// <summary>Where the runner sees the shared storage. Each job gets <c>octopus-job-&lt;job&gt;</c> under it.</summary>
    public string LocalPath { get; set; } = "/octopus-shared";

    /// <summary>Leaves each job's directory behind when it finishes, for looking at what the steps wrote.</summary>
    public bool KeepState { get; set; }
}

/// <summary>
/// AKS Pod Sandboxing: each pod runs in its own Kata VM, with its own kernel, on a node pool
/// created with <c>workloadRuntime: KataVmIsolation</c>.
/// </summary>
public sealed class SandboxOptions
{
    public bool Enabled { get; set; }

    public string RuntimeClassName { get; set; } = "kata-vm-isolation";

    public string CpuRequest { get; set; } = "10m";

    /// <summary>Sets the pod VM's vCPU count. Without a CPU limit the VM gets one vCPU.</summary>
    public string CpuLimit { get; set; } = "1";

    /// <summary>
    /// Keep equal to <see cref="MemoryLimit"/>. The pod VM really uses its whole memory size, so a lower
    /// request lets the scheduler overpack nodes and risks evictions. The RuntimeClass adds Kata's host overhead on top.
    /// </summary>
    public string MemoryRequest { get; set; } = "256Mi";

    /// <summary>Sets the pod VM's memory. AKS doesn't support pod VMs below 128Mi, and defaults to 512Mi without a limit.</summary>
    public string MemoryLimit { get; set; } = "256Mi";

    public Dictionary<string, string> NodeSelector { get; set; } = [];

    public List<TolerationOptions> Tolerations { get; set; } = [];
}

public sealed class TolerationOptions
{
    public string Key { get; set; } = "";
    public string Operator { get; set; } = "Equal";
    public string? Value { get; set; }
    public string Effect { get; set; } = "NoSchedule";
}
