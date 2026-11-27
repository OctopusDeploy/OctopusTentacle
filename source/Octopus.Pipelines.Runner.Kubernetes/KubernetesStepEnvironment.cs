using System.Globalization;
using System.Text;
using System.Text.Json;
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
/// Runs a job's steps in one long-lived pod per image, through the exec API, so steps that share an image share a
/// container. Each pod has the job pod's hardening; init containers copy in the job's step runners; the step runner,
/// idle, is the main process; <c>shareProcessNamespace</c> makes the pause container PID 1, so orphans are reaped. The
/// job's directory on shared storage is mounted in. Services run in pods of their own for the whole job; steps reach
/// them by name through <c>hostAliases</c>. The runner needs <c>pods/exec</c>.
/// </summary>
sealed partial class KubernetesStepEnvironment(
    IKubernetes client,
    KubernetesJobBackend backend,
    KubernetesBackendOptions settings,
    GatewayOptions gateway,
    ILogger logger) : IStepEnvironment
{
    const string StepRunnersVolume = "step-runners";
    const string SharedVolume = "shared";
    const string TmpVolume = "tmp";
    const string JobIdLabel = "poc.octopus.com/job-id";
    const string RoleLabel = "poc.octopus.com/role";

    readonly IKubernetes client = client;
    readonly KubernetesJobBackend backend = backend;
    readonly KubernetesBackendOptions settings = settings;
    readonly GatewayOptions gateway = gateway;
    readonly ILogger logger = logger;

    public async Task<IStepSession> StartAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        var steps = settings.Steps;
        if (steps.ClaimName is null == steps.HostPath is null)
        {
            throw new InvalidOperationException("Jobs with steps need shared storage: set exactly one of Runner:Kubernetes:Steps:ClaimName and HostPath.");
        }

        var directoryName = $"octopus-job-{job.JobId}";
        var directory = Path.Combine(steps.LocalPath, directoryName);
        SharedStorage.CreateJobDirectory(directory);

        var session = new Session(this, job, output, directory, directoryName);
        try
        {
            await session.CreatePullSecretAsync(cancellationToken);
            await session.StartServicesAsync(cancellationToken);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    /// <summary>A pod that keeps a job's container for one image: the job pod's hardening, the step runners, and the job's files.</summary>
    internal static V1Pod BuildStepPod(
        string podName,
        JobSpec job,
        string image,
        string directoryName,
        IReadOnlyList<V1HostAlias> services,
        string? pullSecret,
        KubernetesBackendOptions settings,
        GatewayOptions gateway)
    {
        var pod = KubernetesJobBackend.BuildPod(podName, job, settings, gateway);
        pod.Metadata.Labels[RoleLabel] = "step-container";

        var spec = pod.Spec;
        spec.ShareProcessNamespace = true;
        spec.HostAliases = services.Count > 0 ? services.ToList() : null;
        spec.ImagePullSecrets = pullSecret is null ? null : [new V1LocalObjectReference { Name = pullSecret }];
        spec.Volumes =
        [
            new V1Volume { Name = StepRunnersVolume, EmptyDir = new V1EmptyDirVolumeSource { SizeLimit = new ResourceQuantity("64Mi") } },
            new V1Volume { Name = TmpVolume, EmptyDir = new V1EmptyDirVolumeSource { SizeLimit = new ResourceQuantity("64Mi") } },
            SharedStorageVolume(settings.Steps),
        ];

        var container = spec.Containers.Single();
        spec.InitContainers = job.StepRunnerImages
            .Select((stepRunnerImage, key) => new V1Container
            {
                Name = $"install-step-runner-{key}",
                Image = stepRunnerImage,
                ImagePullPolicy = "IfNotPresent",
                Command = ["/step-runner", "install", $"{StepLayout.StepRunnersPath}/{key}"],
                VolumeMounts = [new V1VolumeMount { Name = StepRunnersVolume, MountPath = StepLayout.StepRunnersPath }],
                Resources = new V1ResourceRequirements
                {
                    Requests = new Dictionary<string, ResourceQuantity> { ["cpu"] = new("10m"), ["memory"] = new("16Mi") },
                    Limits = new Dictionary<string, ResourceQuantity> { ["cpu"] = new("250m"), ["memory"] = new("32Mi") },
                },
                SecurityContext = container.SecurityContext,
            })
            .ToList();

        // The image's own entrypoint and command don't run: the step runner, idle, keeps the container alive.
        container.Image = image;
        container.Command = StepLayout.IdleCommand(0).ToList();
        container.Args = null;
        container.WorkingDir = StepLayout.WorkspacePath;
        container.Env =
        [
            new V1EnvVar { Name = "HOME", Value = "/tmp" },
            new V1EnvVar { Name = StepEnvironment.Workspace, Value = StepLayout.WorkspacePath },
        ];
        container.VolumeMounts =
        [
            new V1VolumeMount { Name = StepRunnersVolume, MountPath = StepLayout.StepRunnersPath, ReadOnlyProperty = true },
            new V1VolumeMount { Name = TmpVolume, MountPath = "/tmp" },
            new V1VolumeMount { Name = SharedVolume, MountPath = StepLayout.WorkspacePath, SubPath = $"{directoryName}/{StepLayout.WorkspaceDirectory}" },
            new V1VolumeMount { Name = SharedVolume, MountPath = StepLayout.StepsPath, SubPath = $"{directoryName}/{StepLayout.StepsDirectory}" },
            new V1VolumeMount { Name = SharedVolume, MountPath = StepLayout.ResultsPath, SubPath = $"{directoryName}/{StepLayout.ResultsDirectory}", ReadOnlyProperty = true },
        ];
        return pod;
    }

    static V1Volume SharedStorageVolume(StepsOptions steps) => steps.ClaimName is { } claim
        ? new V1Volume { Name = SharedVolume, PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource { ClaimName = claim } }
        : new V1Volume { Name = SharedVolume, HostPath = new V1HostPathVolumeSource { Path = steps.HostPath, Type = "Directory" } };

    /// <summary>A service's pod: its image runs as it was built to, with the job's limits and no new privileges.</summary>
    internal static V1Pod BuildServicePod(string podName, JobSpec job, ServiceSpec service, string? pullSecret, KubernetesBackendOptions settings, GatewayOptions gateway) => new()
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
            ImagePullSecrets = pullSecret is null ? null : [new V1LocalObjectReference { Name = pullSecret }],
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

    /// <summary>A docker config JSON for the job's registry credentials, as an image pull secret's data.</summary>
    internal static byte[] DockerConfigJson(IReadOnlyList<RegistryCredential> credentials)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("auths");
            foreach (var credential in credentials)
            {
                json.WriteStartObject(credential.Registry is "docker.io" or "index.docker.io" ? "https://index.docker.io/v1/" : credential.Registry);
                json.WriteString("auth", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Password}")));
                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>A DNS label: lowercase letters, digits and dashes.</summary>
    static string Label(string text) => DnsUnsafe().Replace(text.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9-]+")]
    private static partial Regex DnsUnsafe();

    sealed class Session(KubernetesStepEnvironment owner, JobSpec job, IJobOutput output, string directory, string directoryName) : IStepSession
    {
        readonly Lock gate = new();
        readonly Dictionary<string, string> pods = [];
        readonly List<string> servicePods = [];
        readonly List<V1HostAlias> hostAliases = [];
        string? policy;
        string? pullSecret;
        int podCount;

        public string Directory => directory;

        string Namespace => owner.settings.Namespace;

        string Resolve(string image) => image.Length > 0 ? image : owner.settings.JobImage;

        public async Task CreatePullSecretAsync(CancellationToken cancellationToken)
        {
            if (job.RegistryCredentials.Count == 0)
            {
                return;
            }

            var name = $"job-{job.JobId}-registry";
            await owner.client.CoreV1.CreateNamespacedSecretAsync(
                new V1Secret
                {
                    Metadata = new V1ObjectMeta { Name = name, Labels = new Dictionary<string, string> { [JobIdLabel] = job.JobId } },
                    Type = "kubernetes.io/dockerconfigjson",
                    Data = new Dictionary<string, byte[]> { [".dockerconfigjson"] = DockerConfigJson(job.RegistryCredentials) },
                },
                Namespace,
                cancellationToken: cancellationToken);
            pullSecret = name;
        }

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
                await owner.backend.CreatePodAsync(BuildServicePod(podName, job, service, pullSecret, owner.settings, owner.gateway), cancellationToken);
                servicePods.Add(podName);
            }

            // Steps reach a service by name; its pod's IP is only known once it's running.
            for (var i = 0; i < job.Services.Count; i++)
            {
                var service = job.Services[i];
                var ip = await WaitForRunningAsync(servicePods[i], cancellationToken);
                await WaitUntilReadyAsync(service, servicePods[i], cancellationToken);
                hostAliases.Add(new V1HostAlias { Ip = ip, Hostnames = [service.Name] });
                Line($"==> service {service.Name}: ready [{service.Image}]");
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

        async Task WaitUntilReadyAsync(ServiceSpec service, string podName, CancellationToken cancellationToken)
        {
            if (service.ReadinessCommand.Count == 0)
            {
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(owner.settings.Steps.ServiceReadyTimeout);
            try
            {
                while (await owner.client.NamespacedPodExecAsync(podName, Namespace, "service", service.ReadinessCommand, false, static (_, _, _) => Task.CompletedTask, timeout.Token) != 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    $"Service {service.Name} wasn't ready after {owner.settings.Steps.ServiceReadyTimeout}: `{string.Join(' ', service.ReadinessCommand)}` kept failing.");
            }
        }

        public async Task<int> ExecAsync(string image, IReadOnlyList<string> command, CancellationToken cancellationToken)
        {
            var pod = await PodForAsync(image, cancellationToken);
            output.Started(DateTimeOffset.UtcNow);
            try
            {
                return await owner.client.NamespacedPodExecAsync(
                    pod,
                    Namespace,
                    PodWatcher.ContainerName,
                    command,
                    false,
                    async (stdin, stdout, stderr) =>
                    {
                        stdin.Close();
                        await Task.WhenAll(PumpAsync(stdout), PumpAsync(stderr));
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Stopping the exec leaves its process running: delete the pod itself.
                await ReleaseAsync(image, CancellationToken.None);
                throw;
            }
        }

        async Task PumpAsync(Stream source)
        {
            var buffer = new byte[8192];
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                lock (gate)
                {
                    output.Write(buffer.AsSpan(0, read));
                }
            }
        }

        async Task<string> PodForAsync(string image, CancellationToken cancellationToken)
        {
            if (pods.TryGetValue(image, out var existing))
            {
                return existing;
            }

            var podName = $"job-{job.JobId}-c{podCount++}";
            var tracker = owner.backend.Watcher.Track(podName);
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
            pods[image] = podName;
            await owner.backend.CreatePodAsync(
                BuildStepPod(podName, job, Resolve(image), directoryName, hostAliases, pullSecret, owner.settings, owner.gateway),
                cancellationToken);
            await tracker.Started.WaitAsync(owner.settings.PodStartTimeout, cancellationToken);
            return podName;
        }

        public async Task ReleaseAsync(string image, CancellationToken cancellationToken)
        {
            if (pods.Remove(image, out var pod))
            {
                owner.backend.Watcher.Untrack(pod);
                await owner.backend.DeleteAsync(pod, Namespace);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var pod in pods.Values.Concat(servicePods))
            {
                owner.backend.Watcher.Untrack(pod);
                await owner.backend.DeleteAsync(pod, Namespace);
            }

            pods.Clear();
            if (policy is not null)
            {
                await DeleteQuietlyAsync(() => owner.client.NetworkingV1.DeleteNamespacedNetworkPolicyAsync(policy, Namespace), policy);
            }

            if (pullSecret is not null)
            {
                await DeleteQuietlyAsync(() => owner.client.CoreV1.DeleteNamespacedSecretAsync(pullSecret, Namespace), pullSecret);
            }

            if (!owner.settings.Steps.KeepState && !SharedStorage.TryDelete(directory))
            {
                owner.logger.LogWarning("Couldn't delete the shared directory {Directory} of job {JobId}", directory, job.JobId);
            }
        }

        async Task DeleteQuietlyAsync(Func<Task> delete, string name)
        {
            try
            {
                await delete();
            }
            catch (HttpOperationException e) when (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
            }
            catch (Exception e)
            {
                owner.logger.LogWarning(e, "Failed to delete {Name}", name);
            }
        }

        void Line(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text + "\n");
            lock (gate)
            {
                output.Write(bytes);
            }
        }
    }
}
