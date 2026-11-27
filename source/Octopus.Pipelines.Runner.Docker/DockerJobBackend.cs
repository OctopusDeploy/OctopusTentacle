using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octopus.Pipelines.Runner.Core;
using Octopus.Pipelines.Runner.Core.Steps;

namespace Octopus.Pipelines.Runner.Docker;

/// <summary>
/// Runs each job with <c>docker run --rm</c>: no network, read-only root filesystem, all capabilities dropped, a
/// non-root user, memory/CPU limits and an init process as PID 1, so the job gets signals. Going through the CLI
/// means the daemon (<c>DOCKER_HOST</c>, contexts) and registry credentials are resolved the way the user's own
/// <c>docker</c> commands resolve them. A job with steps runs them by <c>docker exec</c> in a container kept per image
/// (see <see cref="DockerStepEnvironment"/>).
/// </summary>
/// <remarks>
/// <c>docker run</c> exits 125 when Docker itself fails (an unknown user, a missing image, a daemon error), which is
/// reported as an error. 126 and 127 mean "can't execute" and "not found" whether Docker or the job's shell says so,
/// so they're returned as the job's exit code. A job that exits 125 itself is misreported as a Docker failure.
/// </remarks>
public sealed class DockerJobBackend(
    DockerBackendOptions options,
    TimeProvider? clock = null,
    ILogger? logger = null) : IJobBackend, IDisposable
{
    internal const string JobIdLabel = "octopus.job-id";
    const string RunIdLabel = "octopus.run-id";
    const int DockerFailedExitCode = 125;
    const int StopTimeoutSeconds = 5;
    static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    readonly DockerCli docker = new(options.Executable);
    readonly TimeProvider clock = clock ?? TimeProvider.System;
    readonly ILogger logger = logger ?? NullLogger.Instance;

    /// <summary>How long to wait for docker run to exit once its container is stopped. Internal so tests can shrink it.</summary>
    internal TimeSpan DrainTimeout { get; init; } = TimeSpan.FromSeconds(5);

    internal DockerBackendOptions Options => options;

    internal DockerCli Cli => docker;

    internal ILogger Logger => logger;

    public async Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        if (job.Sandboxed)
        {
            // Never run a job that asked for a sandbox without one.
            throw new NotSupportedException("Sandboxed jobs need the Kubernetes backend with Runner:Kubernetes:Sandbox:Enabled.");
        }

        if (job.Steps.Count > 0)
        {
            return await new StepsOrchestrator(new DockerStepEnvironment(this), clock).RunAsync(job, output, cancellationToken);
        }

        await EnsureImageAsync(options.JobImage, cancellationToken);
        return await RunContainerAsync(
            job,
            [.. HardeningArgs(job), options.JobImage, "sh", "-c", job.Command],
            output,
            new Lock(),
            () => output.Started(clock.GetUtcNow()),
            cancellationToken);
    }

    public void Dispose()
    {
    }

    internal DateTimeOffset Now => clock.GetUtcNow();

    /// <summary>Where jobs with steps keep their state, and where the step runner is staged.</summary>
    internal string StateRoot => Path.GetFullPath(options.StateRoot ?? Path.GetTempPath());

    /// <summary>
    /// Pulls the image unless it's present, so a slow pull isn't counted as the job's start, and a failed one isn't a
    /// 125 from run. <paramref name="dockerConfig"/> is a CLI config directory with the job's registry credentials.
    /// </summary>
    internal async Task EnsureImageAsync(string image, CancellationToken cancellationToken, string? dockerConfig = null)
    {
        if (!(await docker.RunAsync(["image", "inspect", "--format", "{{.Id}}", image], cancellationToken)).Succeeded)
        {
            string[] config = dockerConfig is null ? [] : ["--config", dockerConfig];
            (await docker.RunAsync([.. config, "pull", "--quiet", image], cancellationToken)).EnsureSuccess();
        }
    }

    /// <summary>The lock-down every job container gets, and the labels that find it again.</summary>
    internal List<string> HardeningArgs(JobSpec job, string network = "none")
    {
        List<string> args =
        [
            "--pull", "never",
            "--network", network,
            "--user", options.User,
            "--memory", options.MemoryLimitBytes.ToString(CultureInfo.InvariantCulture),
            "--cpus", options.CpuLimit.ToString(CultureInfo.InvariantCulture),
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges",
            "--label", $"{JobIdLabel}={job.JobId}",
            "--label", $"{RunIdLabel}={job.RunId}",
        ];
        if (options.ReadOnlyRootFilesystem)
        {
            args.Add("--read-only");
        }

        return args;
    }

    /// <summary>
    /// Runs one container to completion with <c>docker run --rm --init</c> and the given arguments (hardening, image and
    /// command), streaming its output under <paramref name="gate"/>, and returns its exit code. Cancelling stops the
    /// container; it never outlives the call.
    /// </summary>
    internal async Task<int> RunContainerAsync(
        JobSpec job,
        IReadOnlyList<string> containerArgs,
        IJobOutput output,
        Lock gate,
        Action started,
        CancellationToken cancellationToken)
    {
        // docker run writes the container's ID here once it has created it: the only handle on the container if the
        // run has to be stopped, or if the CLI dies before --rm has removed it.
        var cidFile = Path.Combine(Path.GetTempPath(), $"octopus-pipelines-{Guid.NewGuid():N}.cid");
        Process? run = null;
        Task? pumps = null;
        var exited = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            run = docker.Start(["run", "--rm", "--init", "--cidfile", cidFile, .. containerArgs]);
            started();

            // stdout and stderr arrive on separate pipes, so they're interleaved at chunk granularity. Docker's own
            // errors arrive on stderr too, so they end up in the job's log.
            var stderrTail = new Tail();
            pumps = Task.WhenAll(
                PumpAsync(run.StandardOutput.BaseStream, output, gate, null),
                PumpAsync(run.StandardError.BaseStream, output, gate, stderrTail));

            await run.WaitForExitAsync(cancellationToken);
            exited = true;

            // The pipes close when the container exits; drain them so no output arrives after we return.
            await pumps;

            if (run.ExitCode == DockerFailedExitCode)
            {
                throw new InvalidOperationException($"Docker couldn't run the container for job {job.JobId}: {stderrTail.DockerError()}");
            }

            return run.ExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && run is not null)
        {
            // Stop the container rather than kill the CLI: killing docker run leaves the container running. Once
            // stopped, --rm removes it and docker run exits by itself.
            if (ReadContainerId(cidFile) is { } id)
            {
                using var cleanup = new CancellationTokenSource(CleanupTimeout);
                try
                {
                    await docker.RunAsync(["stop", "--time", StopTimeoutSeconds.ToString(CultureInfo.InvariantCulture), id], cleanup.Token);
                    using var drain = new CancellationTokenSource(DrainTimeout);
                    await run.WaitForExitAsync(drain.Token);
                    exited = true;
                }
                catch (Exception e) when (e is InvalidOperationException or OperationCanceledException)
                {
                }
            }

            throw;
        }
        finally
        {
            if (run is not null)
            {
                // Killing the CLI closes its pipes, which ends the pumps; they never outlive the run.
                DockerCli.Kill(run);
                if (pumps is not null)
                {
                    await pumps.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }

                run.Dispose();
            }

            // When docker run exits by itself, --rm has already removed the container, so there's nothing to do.
            // Otherwise the CLI was killed, and the container may be left behind.
            if (run is not null && !exited)
            {
                await RemoveLeftoversAsync(ReadContainerId(cidFile), job.JobId);
            }

            TryDelete(cidFile);
        }
    }

    /// <summary>Runs a short-lived CLI command (e.g. <c>docker exec</c>), streaming its output, and returns its exit code.</summary>
    internal async Task<int> StreamAsync(IReadOnlyList<string> args, IJobOutput output, Lock gate, CancellationToken cancellationToken)
    {
        using var process = docker.Start(args);
        using var kill = cancellationToken.Register(static state => DockerCli.Kill((Process)state!), process);
        var pumps = Task.WhenAll(
            PumpAsync(process.StandardOutput.BaseStream, output, gate, null),
            PumpAsync(process.StandardError.BaseStream, output, gate, null));
        await process.WaitForExitAsync(CancellationToken.None);
        await pumps;
        cancellationToken.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    /// <summary>Removes the job's containers by ID, or by the job's label when there's no ID.</summary>
    internal async Task RemoveLeftoversAsync(string? id, string jobId)
    {
        using var cleanup = new CancellationTokenSource(CleanupTimeout);
        try
        {
            IEnumerable<string> ids = id is not null
                ? [id]
                : (await docker.RunAsync(["ps", "--all", "--quiet", "--filter", $"label={JobIdLabel}={jobId}"], cleanup.Token)).EnsureSuccess()
                    .Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var leftover in ids)
            {
                var result = await docker.RunAsync(["rm", "--force", leftover], cleanup.Token);
                if (!result.Succeeded && !result.Stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase))
                {
                    result.EnsureSuccess();
                }
            }
        }
        catch (Exception e)
        {
            // Never thrown: it must not replace the job's real outcome (its exit code, error or cancellation).
            logger.LogWarning(e, "Failed to remove the container for job {JobId}", jobId);
        }
    }

    static string? ReadContainerId(string cidFile)
    {
        try
        {
            return File.ReadAllText(cidFile).Trim() is { Length: > 0 } id ? id : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    static async Task PumpAsync(Stream source, IJobOutput output, Lock gate, Tail? tail)
    {
        var buffer = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            lock (gate)
            {
                output.Write(buffer.AsSpan(0, read));
            }

            tail?.Append(buffer.AsSpan(0, read));
        }
    }

    /// <summary>The last few KB of stderr, to name Docker's error when docker run exits 125.</summary>
    sealed class Tail
    {
        const int Capacity = 4096;
        readonly Lock gate = new();
        byte[] bytes = [];

        public void Append(ReadOnlySpan<byte> data)
        {
            lock (gate)
            {
                var keep = Math.Min(bytes.Length, Capacity - Math.Min(data.Length, Capacity));
                var next = new byte[keep + Math.Min(data.Length, Capacity)];
                bytes.AsSpan(bytes.Length - keep).CopyTo(next);
                data[^Math.Min(data.Length, Capacity)..].CopyTo(next.AsSpan(keep));
                bytes = next;
            }
        }

        /// <summary>Docker's "docker: …" line if there is one, otherwise the last line of stderr.</summary>
        public string DockerError()
        {
            string text;
            lock (gate)
            {
                text = Encoding.UTF8.GetString(bytes);
            }

            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return lines.LastOrDefault(l => l.StartsWith("docker: ", StringComparison.Ordinal))?["docker: ".Length..]
                ?? lines.LastOrDefault()
                ?? "docker run exited 125 with no error message";
        }
    }
}
