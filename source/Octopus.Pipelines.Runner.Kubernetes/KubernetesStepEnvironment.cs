using System.Text;
using System.Text.RegularExpressions;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;
using Octopus.Pipelines.Runner.Gateway;
using Octopus.Pipelines.Runner.Steps;

namespace Octopus.Pipelines.Runner.Kubernetes;

/// <summary>
/// Runs each step in its own pod: the job pod's hardening, plus an init container that copies the step runner into an
/// <c>emptyDir</c>, the step runner as the job container's entry point, and the job's directory on shared storage at
/// <c>/octopus</c>. <c>shareProcessNamespace</c> makes the pod's pause container PID 1, so orphaned processes are reaped.
/// Services run in pods of their own for the whole job; step pods reach them by name through <c>hostAliases</c>.
/// </summary>
sealed partial class KubernetesStepEnvironment(
    IKubernetes client,
    KubernetesJobBackend backend,
    KubernetesBackendOptions settings,
    GatewayOptions gateway,
    ILogger logger) : IStepEnvironment
{
    readonly IKubernetes client = client;
    readonly KubernetesJobBackend backend = backend;
    readonly KubernetesBackendOptions settings = settings;
    readonly GatewayOptions gateway = gateway;
    readonly ILogger logger = logger;

    const string StepRunnerVolume = "step-runner";
    const string SharedVolume = "shared";
    const string TmpVolume = "tmp";
    const string JobIdLabel = "poc.octopus.com/job-id";
    const string RoleLabel = "poc.octopus.com/role";

    public async Task<IStepSession> StartAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        var steps = settings.Steps;
        if (steps.ClaimName is null == steps.HostPath is null)
        {
            throw new InvalidOperationException("Jobs with steps need shared storage: set exactly one of Runner:Kubernetes:Steps:ClaimName and HostPath.");
        }

        var directoryName = $"octopus-job-{job.JobId}";
        var directory = Path.Combine(steps.LocalPath, directoryName);
        SharedStorage.CreateDirectory(directory);
        SharedStorage.CreateDirectory(Path.Combine(directory, StepLayout.WorkspaceDirectory));
        SharedStorage.CreateDirectory(Path.Combine(directory, "steps"));

        var session = new Session(this, job, output, directory, directoryName);
        try
        {
            await session.StartServicesAsync(cancellationToken);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    /// <summary>A step's pod: the job pod's hardening, the step runner, and the job's shared directory.</summary>
    internal static V1Pod BuildStepPod(
        string podName,
        JobSpec job,
        int index,
        StepSpec step,
        string directoryName,
        IReadOnlyList<V1HostAlias> services,
        KubernetesBackendOptions settings,
        GatewayOptions gateway)
    {
        var pod = KubernetesJobBackend.BuildPod(podName, job, settings, gateway);
        pod.Metadata.Labels["poc.octopus.com/step-index"] = index.ToString();
        pod.Metadata.Labels[RoleLabel] = "step";

        var spec = pod.Spec;
        spec.ShareProcessNamespace = true;
        spec.HostAliases = services.Count > 0 ? services.ToList() : null;
        spec.Volumes =
        [
            new V1Volume { Name = StepRunnerVolume, EmptyDir = new V1EmptyDirVolumeSource { SizeLimit = new ResourceQuantity("32Mi") } },
            new V1Volume { Name = TmpVolume, EmptyDir = new V1EmptyDirVolumeSource { SizeLimit = new ResourceQuantity("64Mi") } },
            SharedStorageVolume(settings.Steps),
        ];

        var container = spec.Containers.Single();
        var hardening = container.SecurityContext;
        spec.InitContainers =
        [
            new V1Container
            {
                Name = "install-step-runner",
                Image = settings.Steps.StepRunnerImage,
                ImagePullPolicy = "IfNotPresent",
                Command = ["/step-runner", "install", StepLayout.StepRunnerDirectory],
                VolumeMounts = [new V1VolumeMount { Name = StepRunnerVolume, MountPath = StepLayout.StepRunnerDirectory }],
                Resources = new V1ResourceRequirements
                {
                    Requests = new Dictionary<string, ResourceQuantity> { ["cpu"] = new("10m"), ["memory"] = new("16Mi") },
                    Limits = new Dictionary<string, ResourceQuantity> { ["cpu"] = new("250m"), ["memory"] = new("32Mi") },
                },
                SecurityContext = hardening,
            },
        ];

        // The image's own entrypoint and command don't run: the step runner runs the step's script.
        container.Image = step.Image ?? settings.JobImage;
        container.Command = [StepLayout.StepRunnerPath];
        container.Args = null;
        container.WorkingDir = $"{StepLayout.MountPath}/{StepLayout.WorkspaceDirectory}";
        container.Env = StepLayout.Variables(index)
            .Select(v => new V1EnvVar { Name = v.Key, Value = v.Value })
            .Append(new V1EnvVar { Name = "HOME", Value = "/tmp" })
            .ToList();
        container.VolumeMounts =
        [
            new V1VolumeMount { Name = StepRunnerVolume, MountPath = StepLayout.StepRunnerDirectory, ReadOnlyProperty = true },
            new V1VolumeMount { Name = TmpVolume, MountPath = "/tmp" },
            new V1VolumeMount { Name = SharedVolume, MountPath = StepLayout.MountPath, SubPath = directoryName },
        ];
        return pod;
    }

    static V1Volume SharedStorageVolume(StepsOptions steps) => steps.ClaimName is { } claim
        ? new V1Volume { Name = SharedVolume, PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource { ClaimName = claim } }
        : new V1Volume { Name = SharedVolume, HostPath = new V1HostPathVolumeSource { Path = steps.HostPath, Type = "Directory" } };

    /// <summary>A service's pod: its image runs as it was built to, with the job's limits and no new privileges.</summary>
    internal static V1Pod BuildServicePod(string podName, JobSpec job, ServiceSpec service, KubernetesBackendOptions settings, GatewayOptions gateway) => new()
    {
        Metadata = new V1ObjectMeta
        {
            Name = podName,
            Labels = new Dictionary<string, string>
            {
                ["app.kubernetes.io/managed-by"] = "targetscalepoc-runner",
                [PodWatcher.RunnerLabel] = gateway.RunnerId,
                ["poc.octopus.com/run-id"] = job.RunId,
                [JobIdLabel] = job.JobId,
                [RoleLabel] = "service",
                ["poc.octopus.com/service"] = service.Name,
            },
        },
        Spec = new V1PodSpec
        {
            RestartPolicy = "Never",
            AutomountServiceAccountToken = false,
            EnableServiceLinks = false,
            TerminationGracePeriodSeconds = 0,
            NodeSelector = settings.NodeSelector.Count > 0 ? settings.NodeSelector : null,
            Containers =
            [
                new V1Container
                {
                    Name = "service",
                    Image = service.Image,
                    ImagePullPolicy = "IfNotPresent",
                    Env = service.Env.Select(e => new V1EnvVar { Name = e.Key, Value = e.Value }).ToList(),
                    Resources = new V1ResourceRequirements
                    {
                        Limits = new Dictionary<string, ResourceQuantity>
                        {
                            ["cpu"] = new(settings.CpuLimit),
                            ["memory"] = new(settings.MemoryLimit),
                        },
                    },
                    SecurityContext = new V1SecurityContext { AllowPrivilegeEscalation = false, Privileged = false },
                },
            ],
        },
    };

    /// <summary>
    /// Lets the job's steps reach its services, and nothing else reach them, in a namespace whose policies would
    /// otherwise block pod-to-pod traffic.
    /// </summary>
    internal static V1NetworkPolicy BuildServicePolicy(string name, JobSpec job) => new()
    {
        Metadata = new V1ObjectMeta { Name = name, Labels = new Dictionary<string, string> { [JobIdLabel] = job.JobId } },
        Spec = new V1NetworkPolicySpec
        {
            PodSelector = new V1LabelSelector { MatchLabels = new Dictionary<string, string> { [JobIdLabel] = job.JobId } },
            PolicyTypes = ["Ingress", "Egress"],
            Ingress = [new V1NetworkPolicyIngressRule { FromProperty = [JobPods(job)] }],
            Egress = [new V1NetworkPolicyEgressRule { To = [JobPods(job)] }],
        },
    };

    static V1NetworkPolicyPeer JobPods(JobSpec job) =>
        new() { PodSelector = new V1LabelSelector { MatchLabels = new Dictionary<string, string> { [JobIdLabel] = job.JobId } } };

    /// <summary>A DNS label: lowercase letters, digits and dashes.</summary>
    static string Label(string text) => DnsUnsafe().Replace(text.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9-]+")]
    private static partial Regex DnsUnsafe();

    sealed class Session(KubernetesStepEnvironment owner, JobSpec job, IJobOutput output, string directory, string directoryName) : IStepSession
    {
        readonly List<string> servicePods = [];
        readonly List<V1HostAlias> hostAliases = [];
        string? policy;

        public string Directory => directory;

        string Namespace => owner.settings.Namespace;

        public async Task StartServicesAsync(CancellationToken cancellationToken)
        {
            if (job.Services.Count == 0)
            {
                return;
            }

            policy = $"job-{job.JobId}-services";
            await owner.client.NetworkingV1.CreateNamespacedNetworkPolicyAsync(BuildServicePolicy(policy, job), Namespace, cancellationToken: cancellationToken);

            foreach (var service in job.Services)
            {
                var podName = $"svc-{job.JobId}-{Label(service.Name)}";
                await owner.client.CoreV1.CreateNamespacedPodAsync(
                    BuildServicePod(podName, job, service, owner.settings, owner.gateway), Namespace, cancellationToken: cancellationToken);
                servicePods.Add(podName);
            }

            // Steps reach a service by name; its pod's IP is only known once it's running.
            for (var i = 0; i < job.Services.Count; i++)
            {
                var ip = await WaitForRunningAsync(servicePods[i], cancellationToken);
                hostAliases.Add(new V1HostAlias { Ip = ip, Hostnames = [job.Services[i].Name] });
                Line($"==> service {job.Services[i].Name}: started [{job.Services[i].Image}]");
            }
        }

        async Task<string> WaitForRunningAsync(string podName, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(owner.settings.PodStartTimeout);
            while (true)
            {
                var pod = await owner.client.CoreV1.ReadNamespacedPodStatusAsync(podName, Namespace, cancellationToken: timeout.Token);
                if (pod.Status?.Phase == "Running" && pod.Status.PodIP is { Length: > 0 } ip)
                {
                    return ip;
                }

                if (pod.Status?.Phase is "Failed" or "Succeeded"
                    || pod.Status?.ContainerStatuses?.FirstOrDefault()?.State?.Waiting is { Reason: "ErrImagePull" or "ImagePullBackOff" or "InvalidImageName" })
                {
                    throw new InvalidOperationException($"Service pod {podName} didn't start: {pod.Status?.Phase} {pod.Status?.ContainerStatuses?.FirstOrDefault()?.State?.Waiting?.Reason}".Trim());
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
            }
        }

        public Task<int> RunStepAsync(int index, StepSpec step, CancellationToken cancellationToken)
        {
            var pod = BuildStepPod($"job-{job.JobId}-{index}", job, index, step, directoryName, hostAliases, owner.settings, owner.gateway);
            return owner.backend.RunPodAsync(pod, output, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var pod in servicePods)
            {
                await owner.backend.DeleteAsync(pod, Namespace);
            }

            if (policy is not null)
            {
                try
                {
                    await owner.client.NetworkingV1.DeleteNamespacedNetworkPolicyAsync(policy, Namespace);
                }
                catch (HttpOperationException e) when (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                }
                catch (Exception e)
                {
                    owner.logger.LogWarning(e, "Failed to delete network policy {Policy}", policy);
                }
            }

            if (!owner.settings.Steps.KeepState && !SharedStorage.TryDelete(directory))
            {
                owner.logger.LogWarning("Couldn't delete the shared directory {Directory} of job {JobId}", directory, job.JobId);
            }
        }

        void Line(string text) => output.Write(Encoding.UTF8.GetBytes(text + "\n"));
    }
}
