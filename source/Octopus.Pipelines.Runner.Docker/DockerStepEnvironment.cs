using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;
using Octopus.Pipelines.Runner.Steps;

namespace Octopus.Pipelines.Runner.Docker;

/// <summary>
/// Runs a job's steps in one long-lived container per image, by <c>docker exec</c>, so steps that share an image share
/// a container: what one step installs outside the workspace, the next one still has. Each container's main process is
/// the step runner, idle, under <c>--init</c>, with the job's hardening. Step runners are extracted from their images
/// with <c>docker create</c> and <c>docker cp</c>. A job with services gets its own internal network: steps reach
/// services by name and nothing else. A job without services has no network at all.
/// </summary>
sealed class DockerStepEnvironment(DockerJobBackend backend) : IStepEnvironment
{
    /// <summary>Where a step runner image keeps the step runner.</summary>
    const string StepRunnerInImage = "/step-runner";

    public async Task<IStepSession> StartAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        var options = backend.Options;
        var directory = Path.Combine(backend.StateRoot, $"octopus-job-{job.JobId}");
        SharedStorage.CreateJobDirectory(directory);
        var session = new Session(backend, job, output, directory);
        try
        {
            session.DockerConfig = WriteRegistryCredentials(job, directory);

            // Pull everything first, so a slow pull isn't counted against a step's timeout.
            var images = job.StepRunnerImages
                .Concat(job.Steps.Select(s => session.Resolve(job.ImageFor(s))))
                .Concat(job.Services.Select(s => s.Image))
                .Distinct();
            foreach (var image in images)
            {
                await backend.EnsureImageAsync(image, cancellationToken, session.DockerConfig);
            }

            for (var key = 0; key < job.StepRunnerImages.Count; key++)
            {
                await ExtractStepRunnerAsync(job.StepRunnerImages[key], Path.Combine(directory, "bin", key.ToString(CultureInfo.InvariantCulture)), cancellationToken);
            }

            await session.StartServicesAsync(cancellationToken);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    /// <summary>Copies the step runner out of its image, without running anything from it.</summary>
    async Task ExtractStepRunnerAsync(string image, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        var created = (await backend.Cli.RunAsync(["create", "--label", $"{DockerJobBackend.JobIdLabel}=extract", image], cancellationToken)).EnsureSuccess();
        var container = created.Stdout.Trim();
        try
        {
            (await backend.Cli.RunAsync(["cp", $"{container}:{StepRunnerInImage}", Path.Combine(destination, StepLayout.StepRunnerFileName)], cancellationToken))
                .EnsureSuccess();
        }
        finally
        {
            await backend.RemoveLeftoversAsync(container, "extract");
        }
    }

    /// <summary>
    /// A Docker CLI config directory holding only the job's registry credentials, for its pulls, or null. The user's
    /// own config, and the credentials in it, are never touched.
    /// </summary>
    static string? WriteRegistryCredentials(JobSpec job, string directory)
    {
        if (job.RegistryCredentials.Count == 0)
        {
            return null;
        }

        var auths = job.RegistryCredentials.ToDictionary(
            c => c.Registry is "docker.io" or "index.docker.io" ? "https://index.docker.io/v1/" : c.Registry,
            c => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.Username}:{c.Password}")));
        var config = Path.Combine(directory, "docker-config");
        Directory.CreateDirectory(config);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(config, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        using var file = File.Create(Path.Combine(config, "config.json"));
        using var json = new Utf8JsonWriter(file);
        json.WriteStartObject();
        json.WriteStartObject("auths");
        foreach (var (registry, auth) in auths)
        {
            json.WriteStartObject(registry);
            json.WriteString("auth", auth);
            json.WriteEndObject();
        }

        json.WriteEndObject();
        json.WriteEndObject();
        return config;
    }

    sealed class Session(DockerJobBackend backend, JobSpec job, IJobOutput output, string directory) : IStepSession
    {
        readonly Lock gate = new();
        readonly Dictionary<string, string> containers = [];
        readonly List<string> services = [];
        string? network;

        public string Directory => directory;

        public string? DockerConfig { get; set; }

        /// <summary>An image key from <see cref="JobSpec.ImageFor"/>, with "" meaning the backend's default.</summary>
        public string Resolve(string image) => image.Length > 0 ? image : backend.Options.JobImage;

        public async Task StartServicesAsync(CancellationToken cancellationToken)
        {
            if (job.Services.Count == 0)
            {
                return;
            }

            // --internal: steps and services reach each other, but not the internet.
            network = $"octopus-job-{job.JobId}";
            (await backend.Cli.RunAsync(
                ["network", "create", "--internal", "--label", $"{DockerJobBackend.JobIdLabel}={job.JobId}", network],
                cancellationToken)).EnsureSuccess();

            foreach (var service in job.Services)
            {
                // A service is a daemon (a database, a cache) that runs as its image intends: its own user, entrypoint
                // and writable filesystem. It still gets the job's limits and no new privileges.
                List<string> args =
                [
                    "run", "--detach", "--init",
                    "--network", network,
                    "--network-alias", service.Name,
                    "--memory", backend.Options.MemoryLimitBytes.ToString(CultureInfo.InvariantCulture),
                    "--cpus", backend.Options.CpuLimit.ToString(CultureInfo.InvariantCulture),
                    "--security-opt", "no-new-privileges",
                    "--label", $"{DockerJobBackend.JobIdLabel}={job.JobId}",
                    "--label", $"octopus.service={service.Name}",
                ];
                foreach (var (name, value) in service.Env)
                {
                    args.AddRange(["--env", $"{name}={value}"]);
                }

                var started = await backend.Cli.RunAsync([.. args, service.Image], cancellationToken);
                var id = started.EnsureSuccess().Stdout.Trim();
                services.Add(id);
                await WaitUntilReadyAsync(service, id, cancellationToken);
                Line($"==> service {service.Name}: ready [{service.Image}]");
            }
        }

        async Task WaitUntilReadyAsync(ServiceSpec service, string container, CancellationToken cancellationToken)
        {
            if (service.ReadinessCommand.Count == 0)
            {
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(backend.Options.ServiceReadyTimeout);
            try
            {
                while (!(await backend.Cli.RunAsync(["exec", container, .. service.ReadinessCommand], timeout.Token)).Succeeded)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    $"Service {service.Name} wasn't ready after {backend.Options.ServiceReadyTimeout}: `{string.Join(' ', service.ReadinessCommand)}` kept failing.");
            }
        }

        public async Task<int> ExecAsync(string image, IReadOnlyList<string> command, CancellationToken cancellationToken)
        {
            var container = await ContainerForAsync(image, cancellationToken);
            output.Started(backend.Now);
            try
            {
                return await backend.StreamAsync(["exec", container, .. command], output, gate, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Stopping docker exec leaves its process running: stop the container itself.
                await ReleaseAsync(image, CancellationToken.None);
                throw;
            }
        }

        async Task<string> ContainerForAsync(string image, CancellationToken cancellationToken)
        {
            if (containers.TryGetValue(image, out var existing))
            {
                return existing;
            }

            var idle = StepLayout.IdleCommand(0);
            List<string> args =
            [
                "run", "--detach", "--init",
                .. backend.HardeningArgs(job, network ?? "none"),
                "--tmpfs", "/tmp",
                "--volume", $"{Path.Combine(directory, StepLayout.WorkspaceDirectory)}:{StepLayout.WorkspacePath}",
                "--volume", $"{Path.Combine(directory, StepLayout.StepsDirectory)}:{StepLayout.StepsPath}",
                "--volume", $"{Path.Combine(directory, StepLayout.ResultsDirectory)}:{StepLayout.ResultsPath}:ro",
                "--volume", $"{Path.Combine(directory, "bin")}:{StepLayout.StepRunnersPath}:ro",
                "--workdir", StepLayout.WorkspacePath,
                "--env", "HOME=/tmp",
                "--env", $"{StepEnvironment.Workspace}={StepLayout.WorkspacePath}",
                // The image's own entrypoint and command don't run: the step runner, idle, keeps the container alive.
                "--entrypoint", idle[0],
                Resolve(image),
                .. idle.Skip(1),
            ];
            var run = await backend.Cli.RunAsync(args, cancellationToken);
            var id = run.EnsureSuccess().Stdout.Trim();
            containers[image] = id;
            return id;
        }

        public async Task ReleaseAsync(string image, CancellationToken cancellationToken)
        {
            if (containers.Remove(image, out var container))
            {
                await backend.RemoveLeftoversAsync(container, job.JobId);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var container in containers.Values.Concat(services))
            {
                await backend.RemoveLeftoversAsync(container, job.JobId);
            }

            containers.Clear();
            if (network is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    await backend.Cli.RunAsync(["network", "rm", network], cleanup.Token);
                }
                catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
                {
                    backend.Logger.LogWarning(e, "Failed to remove network {Network}", network);
                }
            }

            if (!backend.Options.KeepState && !SharedStorage.TryDelete(directory))
            {
                backend.Logger.LogWarning("Couldn't delete the state directory {Directory} of job {JobId}", directory, job.JobId);
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
