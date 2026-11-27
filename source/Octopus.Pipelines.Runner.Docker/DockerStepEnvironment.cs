using Microsoft.Extensions.Logging;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;
using Octopus.Pipelines.Runner.Steps;

namespace Octopus.Pipelines.Runner.Docker;

/// <summary>
/// Runs each step in its own <c>docker run --rm</c>, with the step runner bind mounted as the entry point and the job's
/// state directory (the shared storage) at <c>/octopus</c>. A job with services gets its own internal network: steps
/// reach services by name and nothing else. A job without services has no network at all.
/// </summary>
sealed class DockerStepEnvironment(DockerJobBackend backend) : IStepEnvironment
{
    public async Task<IStepSession> StartAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        var options = backend.Options;
        var stepRunner = backend.StageStepRunner();

        // Pull everything first, so a slow pull isn't counted against a step's timeout.
        foreach (var image in job.Steps.Select(s => s.Image ?? options.JobImage).Concat(job.Services.Select(s => s.Image)).Distinct())
        {
            await backend.EnsureImageAsync(image, cancellationToken);
        }

        var directory = Path.Combine(backend.StateRoot, $"octopus-job-{job.JobId}");
        SharedStorage.CreateDirectory(directory);
        SharedStorage.CreateDirectory(Path.Combine(directory, StepLayout.WorkspaceDirectory));
        SharedStorage.CreateDirectory(Path.Combine(directory, "steps"));

        var session = new Session(backend, job, output, directory, stepRunner);
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

    sealed class Session(DockerJobBackend backend, JobSpec job, IJobOutput output, string directory, string stepRunner) : IStepSession
    {
        readonly Lock gate = new();
        readonly List<string> services = [];
        string? network;

        public string Directory => directory;

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
                    "run", "--detach", "--rm", "--init",
                    "--network", network,
                    "--network-alias", service.Name,
                    "--memory", backend.Options.MemoryLimitBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--cpus", backend.Options.CpuLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--security-opt", "no-new-privileges",
                    "--label", $"{DockerJobBackend.JobIdLabel}={job.JobId}",
                    "--label", $"octopus.service={service.Name}",
                ];
                foreach (var (name, value) in service.Env)
                {
                    args.AddRange(["--env", $"{name}={value}"]);
                }

                var started = await backend.Cli.RunAsync([.. args, service.Image], cancellationToken);
                services.Add(started.EnsureSuccess().Stdout.Trim());
                Banner($"==> service {service.Name}: started [{service.Image}]");
            }
        }

        public Task<int> RunStepAsync(int index, StepSpec step, CancellationToken cancellationToken)
        {
            List<string> args =
            [
                .. backend.HardeningArgs(job, network ?? "none"),
                "--tmpfs", "/tmp",
                "--volume", $"{directory}:{StepLayout.MountPath}",
                "--volume", $"{stepRunner}:{StepLayout.StepRunnerPath}:ro",
                "--workdir", $"{StepLayout.MountPath}/{StepLayout.WorkspaceDirectory}",
                "--env", "HOME=/tmp",
                "--entrypoint", StepLayout.StepRunnerPath,
            ];
            foreach (var (name, value) in StepLayout.Variables(index))
            {
                args.AddRange(["--env", $"{name}={value}"]);
            }

            args.Add(step.Image ?? backend.Options.JobImage);
            return backend.RunContainerAsync(job, args, output, gate, () => output.Started(backend.Now), cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var service in services)
            {
                await backend.RemoveLeftoversAsync(service, job.JobId);
            }

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

        void Banner(string line)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(line + "\n");
            lock (gate)
            {
                output.Write(bytes);
            }
        }
    }
}
