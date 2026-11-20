using Microsoft.Extensions.Logging;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Gateway;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Options;

namespace Octopus.Pipelines.Runner.Kubernetes;

/// <summary>
/// Runs each job as a bare pod. We avoid batch/v1 Jobs on purpose: the Job controller adds its own
/// API traffic and latency, which would muddy measurements of what one cell can sustain.
/// </summary>
public sealed class KubernetesJobBackend(
    IKubernetes client,
    PodWatcher watcher,
    IOptions<KubernetesBackendOptions> options,
    IOptions<GatewayOptions> gateway,
    ILogger<KubernetesJobBackend> logger) : IJobBackend
{
    readonly SemaphoreSlim createThrottle = new(Math.Max(1, options.Value.MaxParallelPodCreates));

    public async Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (job.Sandboxed && !settings.Sandbox.Enabled)
        {
            // Never fall back to a shared kernel for a job that asked for its own.
            throw new InvalidOperationException("This runner cell has no sandbox node pool (Runner:Kubernetes:Sandbox:Enabled is false).");
        }

        var podName = $"job-{job.JobId}";
        var tracker = watcher.Track(podName);
        tracker.PlacementChanged = waiting =>
        {
            if (waiting)
            {
                output.WaitingForNode();
            }
            else
            {
                output.Placed();
            }
        };
        try
        {
            await createThrottle.WaitAsync(cancellationToken);
            try
            {
                await client.CoreV1.CreateNamespacedPodAsync(BuildPod(podName, job, options.Value, gateway.Value), settings.Namespace, cancellationToken: cancellationToken);
            }
            finally
            {
                createThrottle.Release();
            }

            var startedAt = await tracker.Started.WaitAsync(settings.PodStartTimeout, cancellationToken);
            output.Started(startedAt);

            // Follow the container log until the container exits, reopening the stream if it's cut. The exit code
            // comes from the pod watch, not the log stream. Works for already-finished containers too.
            var follower = new PodLogFollower(
                (sinceSeconds, follow, ct) => client.CoreV1.ReadNamespacedPodLogAsync(
                    podName, settings.Namespace, container: PodWatcher.ContainerName, follow: follow,
                    sinceSeconds: sinceSeconds, timestamps: true, cancellationToken: ct),
                tracker.Terminated,
                TimeProvider.System,
                logger,
                podName);
            await follower.FollowAsync(output, cancellationToken);

            return await tracker.Terminated.WaitAsync(TimeSpan.FromMinutes(2), cancellationToken);
        }
        finally
        {
            watcher.Untrack(podName);
            _ = DeleteAsync(podName, settings.Namespace);
        }
    }

    async Task DeleteAsync(string podName, string ns)
    {
        try
        {
            await client.CoreV1.DeleteNamespacedPodAsync(podName, ns, gracePeriodSeconds: 0);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete pod {Pod}", podName);
        }
    }

    /// <summary>
    /// Builds the job pod. Every job gets the baseline hardening: non-root, no capabilities, read-only root,
    /// RuntimeDefault seccomp, no service account token, CPU/memory/disk limits, and (for standard jobs) its
    /// own user namespace. The poc-jobs namespace adds NetworkPolicies (outbound internet only) and Pod Security "restricted".
    /// Sandboxed jobs also run in their own Kata VM on the sandbox node pool.
    /// </summary>
    internal static V1Pod BuildPod(string podName, JobSpec job, KubernetesBackendOptions settings, GatewayOptions gateway)
    {
        var sandbox = settings.Sandbox;
        var nodeSelector = job.Sandboxed ? sandbox.NodeSelector : settings.NodeSelector;
        var tolerations = job.Sandboxed ? sandbox.Tolerations : settings.Tolerations;
        return new V1Pod
        {
            Metadata = new V1ObjectMeta
            {
                Name = podName,
                Labels = new Dictionary<string, string>
                {
                    ["app.kubernetes.io/managed-by"] = "targetscalepoc-runner",
                    [PodWatcher.RunnerLabel] = gateway.RunnerId,
                    ["poc.octopus.com/cell-id"] = gateway.CellId,
                    ["poc.octopus.com/run-id"] = job.RunId,
                    ["poc.octopus.com/job-id"] = job.JobId,
                    ["poc.octopus.com/isolation"] = job.Sandboxed ? "sandboxed" : "standard",
                },
            },
            Spec = new V1PodSpec
            {
                RuntimeClassName = job.Sandboxed ? sandbox.RuntimeClassName : null,
                // Kata gives the pod its own kernel, and doesn't support user namespaces inside the pod VM.
                HostUsers = !job.Sandboxed && settings.UserNamespaces ? false : null,
                RestartPolicy = "Never",
                AutomountServiceAccountToken = false,
                EnableServiceLinks = false,
                TerminationGracePeriodSeconds = 0,
                NodeSelector = nodeSelector.Count > 0 ? nodeSelector : null,
                Tolerations = tolerations.Count > 0
                    ? tolerations.Select(t => new V1Toleration { Key = t.Key, OperatorProperty = t.Operator, Value = t.Value, Effect = t.Effect }).ToList()
                    : null,
                SecurityContext = new V1PodSecurityContext
                {
                    RunAsNonRoot = true,
                    RunAsUser = 65534,
                    RunAsGroup = 65534,
                    SeccompProfile = new V1SeccompProfile { Type = "RuntimeDefault" },
                },
                Containers =
                [
                    new V1Container
                    {
                        Name = PodWatcher.ContainerName,
                        Image = settings.JobImage,
                        ImagePullPolicy = "IfNotPresent",
                        Command = ["sh", "-c", job.Command],
                        Resources = new V1ResourceRequirements
                        {
                            Requests = new Dictionary<string, ResourceQuantity>
                            {
                                ["cpu"] = new(job.Sandboxed ? sandbox.CpuRequest : settings.CpuRequest),
                                ["memory"] = new(job.Sandboxed ? sandbox.MemoryRequest : settings.MemoryRequest),
                                ["ephemeral-storage"] = new(settings.EphemeralStorageRequest),
                            },
                            Limits = new Dictionary<string, ResourceQuantity>
                            {
                                ["cpu"] = new(job.Sandboxed ? sandbox.CpuLimit : settings.CpuLimit),
                                ["memory"] = new(job.Sandboxed ? sandbox.MemoryLimit : settings.MemoryLimit),
                                ["ephemeral-storage"] = new(settings.EphemeralStorageLimit),
                            },
                        },
                        SecurityContext = new V1SecurityContext
                        {
                            AllowPrivilegeEscalation = false,
                            Privileged = false,
                            ReadOnlyRootFilesystem = true,
                            Capabilities = new V1Capabilities { Drop = ["ALL"] },
                        },
                    },
                ],
            },
        };
    }
}
