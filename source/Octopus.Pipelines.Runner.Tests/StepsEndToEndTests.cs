using System.Diagnostics;
using k8s;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;
using Octopus.Pipelines.Runner.Docker;
using Octopus.Pipelines.Runner.Gateway;
using Octopus.Pipelines.Runner.Kubernetes;
using Octopus.Pipelines.Runner.Workflows;
using RecordingOutput = Octopus.Pipelines.Runner.Tests.StepsOrchestratorTests.RecordingOutput;

namespace Octopus.Pipelines.Runner.Tests;

/// <summary>
/// Runs jobs with steps on the real backends, with the published step runner. Skipped unless the environment says where
/// things are:
/// <list type="bullet">
/// <item>Docker: <c>OCTOPUS_STEP_RUNNER</c>, the statically linked step runner executable.</item>
/// <item>Kubernetes: <c>PIPELINES_KUBE_CONTEXT</c>, <c>PIPELINES_KUBE_NAMESPACE</c>, <c>PIPELINES_SHARED_HOST_PATH</c>
/// (the shared directory on the node) and <c>PIPELINES_SHARED_LOCAL_PATH</c> (the same directory here), with the
/// step runner image <c>octopus/pipelines-step-runner:dev</c> and every image below already on the node.</item>
/// </list>
/// </summary>
public sealed class StepsEndToEndTests
{
    public static TheoryData<string> Backends => ["docker", "kubernetes"];

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Runs_the_same_container_workflow(string backend)
    {
        var (job, output) = await RunWorkflowAsync(backend, "1-same-container.yml");

        AssertReleased(output);
        Assert.Equal(3, Count(output.Text, "Alpine Linux v3.20"));
        await AssertNothingLeftAsync(backend, job);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Runs_the_different_middle_container_workflow(string backend)
    {
        var (job, output) = await RunWorkflowAsync(backend, "2-different-middle-container.yml");

        AssertReleased(output);
        Assert.Equal(2, Count(output.Text, "Alpine Linux v3.20"));
        Assert.Contains("Python 3.12", output.Text);
        await AssertNothingLeftAsync(backend, job);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Runs_the_different_containers_workflow(string backend)
    {
        var (job, output) = await RunWorkflowAsync(backend, "3-different-containers.yml");

        AssertReleased(output);
        Assert.Contains("Debian GNU/Linux 12 (bookworm)", output.Text);
        Assert.Contains("Python 3.12", output.Text);
        Assert.Contains("Alpine Linux v3.20", output.Text);
        await AssertNothingLeftAsync(backend, job);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Skips_steps_after_a_failure_but_runs_always_steps(string backend)
    {
        var job = Job(
            new StepSpec("build", "echo \"artifact=app.tar\" >> \"$OCTOPUS_OUTPUT\"\nexit 4"),
            new StepSpec("deploy", "echo never"),
            new StepSpec("report", "echo \"build was ${{ steps.build.outcome }}, deploy was ${{ steps.deploy.outcome }}, artifact ${{ steps.build.outputs.artifact }}\"")
            {
                If = "always()",
                Image = "debian:bookworm-slim",
            });

        var (exitCode, output) = await RunAsync(backend, job);

        Assert.Equal(4, exitCode);
        Assert.Equal(["build:Failure:4", "deploy:Skipped:", "report:Success:0"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}:{s.ExitCode}"));
        Assert.Contains("build was failure, deploy was skipped, artifact app.tar", output.Text);
        await AssertNothingLeftAsync(backend, job);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Masks_secrets_and_keeps_secret_outputs_inside_the_job(string backend)
    {
        var job = Job(
            new StepSpec("login", "echo \"using $TOKEN\"\necho \"header=Bearer $TOKEN\" >> \"$OCTOPUS_OUTPUT\"\necho user=me >> \"$OCTOPUS_OUTPUT\"")
            {
                Env = new Dictionary<string, string> { ["TOKEN"] = "${{ secrets.DEPLOY_TOKEN }}" },
            },
            new StepSpec("call", "echo \"calling with ${{ steps.login.outputs.header }} as ${{ steps.login.outputs.user }}\"")) with
        {
            Secrets = new Dictionary<string, string> { ["DEPLOY_TOKEN"] = "tok-1234567" },
        };

        var (exitCode, output) = await RunAsync(backend, job);

        Assert.Equal(0, exitCode);
        Assert.Contains("using ***", output.Text);
        Assert.Contains("calling with Bearer *** as me", output.Text);
        Assert.DoesNotContain("tok-1234567", output.Text);
        Assert.Equal(new Dictionary<string, string> { ["user"] = "me" }, output.Steps[0].Outputs);
        await AssertNothingLeftAsync(backend, job);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Stops_a_step_that_runs_past_its_timeout(string backend)
    {
        var job = Job(new StepSpec("slow", "echo started; sleep 120") { Timeout = TimeSpan.FromSeconds(3) }, new StepSpec("next", "echo never"));
        var stopwatch = Stopwatch.StartNew();

        var (exitCode, output) = await RunAsync(backend, job);

        Assert.NotEqual(0, exitCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"took {stopwatch.Elapsed}");
        Assert.Equal("The step timed out after 00:00:03.", output.Steps[0].Error);
        Assert.Equal(StepOutcome.Skipped, output.Steps[1].Outcome);
        await AssertNothingLeftAsync(backend, job);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Lets_steps_reach_services_by_name(string backend)
    {
        const string ping = """
            for i in $(seq 1 40); do
              if reply=$( (printf 'PING\r\n'; sleep 1) | nc redis 6379 2>/dev/null) && [ -n "$reply" ]; then echo "redis said $reply"; exit 0; fi
              sleep 0.5
            done
            echo "redis never answered"; exit 1
            """;
        var job = Job(new StepSpec("ping", ping), new StepSpec("again", ping) { Image = "busybox:1.37" }) with
        {
            Services = [new ServiceSpec("redis", "redis:7-alpine")],
        };

        var (exitCode, output) = await RunAsync(backend, job);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, Count(output.Text, "redis said +PONG"));
        await AssertNothingLeftAsync(backend, job);
    }

    static void AssertReleased(RecordingOutput output)
    {
        Assert.Equal(["generate:Success", "decide:Success", "deploy:Success"], output.Steps.Select(s => $"{s.Id}:{s.Outcome}"));
        Assert.Equal("1.4.0", output.Steps[0].Outputs["version"]);
        Assert.Equal("true", output.Steps[1].Outputs["deploy"]);
        Assert.Equal("Release 1.4.0\ndeploy: true", output.Steps[1].Outputs["notes"]);
        Assert.Equal(2, Count(output.Text, "workspace has: built by generate"));
        Assert.Contains("deploying 1.4.0\nRelease 1.4.0\ndeploy: true\n", output.Text);
    }

    static async Task<(JobSpec Job, RecordingOutput Output)> RunWorkflowAsync(string backend, string file)
    {
        var workflow = WorkflowFile.Load(Path.Combine(AppContext.BaseDirectory, "workflows", file));
        var job = Job(workflow.Steps.ToArray()) with { Env = workflow.Env };
        var (exitCode, output) = await RunAsync(backend, job);
        Assert.Equal(0, exitCode);
        return (job, output);
    }

    static JobSpec Job(params StepSpec[] steps) => new($"e2e{Guid.NewGuid():N}"[..24], "e2e", "") { Steps = steps };

    static async Task<(int ExitCode, RecordingOutput Output)> RunAsync(string backend, JobSpec job)
    {
        var output = new RecordingOutput();
        var exitCode = backend switch
        {
            "docker" => await RunOnDockerAsync(job, output),
            _ => await RunOnKubernetesAsync(job, output),
        };
        return (exitCode, output);
    }

    static async Task<int> RunOnDockerAsync(JobSpec job, RecordingOutput output)
    {
        var stepRunner = Environment.GetEnvironmentVariable("OCTOPUS_STEP_RUNNER");
        Assert.SkipWhen(stepRunner is null, "OCTOPUS_STEP_RUNNER isn't set");
        using var backend = new DockerJobBackend(new DockerBackendOptions { StepRunnerPath = stepRunner, StateRoot = DockerStateRoot });
        return await backend.RunAsync(job, output, TestContext.Current.CancellationToken);
    }

    static string DockerStateRoot => Path.Combine(Path.GetTempPath(), "octopus-steps-e2e");

    static async Task<int> RunOnKubernetesAsync(JobSpec job, RecordingOutput output)
    {
        var kubernetes = Kubernetes.Value;
        Assert.SkipWhen(kubernetes is null, "PIPELINES_KUBE_CONTEXT isn't set");
        return await kubernetes.Backend.RunAsync(job, output, TestContext.Current.CancellationToken);
    }

    static async Task AssertNothingLeftAsync(string backend, JobSpec job)
    {
        if (backend == "docker")
        {
            var containers = Run("docker", "ps", "--all", "--quiet", "--filter", $"label=octopus.job-id={job.JobId}");
            var networks = Run("docker", "network", "ls", "--quiet", "--filter", $"label=octopus.job-id={job.JobId}");
            Assert.Equal("", containers + networks);
            Assert.False(Directory.Exists(Path.Combine(DockerStateRoot, $"octopus-job-{job.JobId}")));
            return;
        }

        var kubernetes = Kubernetes.Value!;
        // Pods are deleted in the background once their step ends.
        for (var i = 0; i < 50; i++)
        {
            var pods = await kubernetes.Client.CoreV1.ListNamespacedPodAsync(
                kubernetes.Options.Namespace, labelSelector: $"poc.octopus.com/job-id={job.JobId}", cancellationToken: TestContext.Current.CancellationToken);
            if (pods.Items.Count == 0)
            {
                break;
            }

            Assert.True(i < 49, $"{pods.Items.Count} pods left behind");
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        var policies = await kubernetes.Client.NetworkingV1.ListNamespacedNetworkPolicyAsync(
            kubernetes.Options.Namespace, labelSelector: $"poc.octopus.com/job-id={job.JobId}", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(policies.Items);
        Assert.False(Directory.Exists(Path.Combine(kubernetes.Options.Steps.LocalPath, $"octopus-job-{job.JobId}")));
    }

    static string Run(string file, params string[] args)
    {
        var startInfo = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return stdout.Trim();
    }

    static int Count(string text, string value) => text.Split(value).Length - 1;

    static readonly Lazy<KubernetesHarness?> Kubernetes = new(KubernetesHarness.Create);

    /// <summary>One pod watcher and backend for every Kubernetes test, as a runner would have.</summary>
    sealed record KubernetesHarness(IKubernetes Client, KubernetesBackendOptions Options, KubernetesJobBackend Backend)
    {
        public static KubernetesHarness? Create()
        {
            var context = Environment.GetEnvironmentVariable("PIPELINES_KUBE_CONTEXT");
            if (context is null)
            {
                return null;
            }

            var client = new k8s.Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(currentContext: context));
            var options = new KubernetesBackendOptions
            {
                Namespace = Environment.GetEnvironmentVariable("PIPELINES_KUBE_NAMESPACE") ?? "pipelines-test",
                // kind's nodes don't have user namespaces turned on.
                UserNamespaces = false,
                MemoryLimit = "64Mi",
                PodStartTimeout = TimeSpan.FromMinutes(2),
                Steps = new StepsOptions
                {
                    HostPath = Environment.GetEnvironmentVariable("PIPELINES_SHARED_HOST_PATH"),
                    LocalPath = Environment.GetEnvironmentVariable("PIPELINES_SHARED_LOCAL_PATH") ?? "/octopus-shared",
                },
            };
            var gateway = new GatewayOptions { ServerAddress = "in-process", CellId = "e2e", RunnerId = $"e2e-{Environment.ProcessId}" };
            var watcher = new PodWatcher(client, Microsoft.Extensions.Options.Options.Create(options), Microsoft.Extensions.Options.Options.Create(gateway), NullLogger<PodWatcher>.Instance);
            watcher.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            var backend = new KubernetesJobBackend(
                client, watcher, Microsoft.Extensions.Options.Options.Create(options), Microsoft.Extensions.Options.Options.Create(gateway), NullLogger<KubernetesJobBackend>.Instance);
            return new KubernetesHarness(client, options, backend);
        }
    }
}
