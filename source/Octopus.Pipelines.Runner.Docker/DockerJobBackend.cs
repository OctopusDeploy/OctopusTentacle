using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octopus.Pipelines.Runner.Core;

namespace Octopus.Pipelines.Runner.Docker;

/// <summary>
/// Runs each job in its own locked-down container on a local Docker daemon: no network, read-only root filesystem,
/// all capabilities dropped, a non-root user and memory/CPU limits. It talks to the Engine API directly over the
/// daemon's socket, so it has no Docker client dependency and is trim/AOT safe.
/// </summary>
public sealed class DockerJobBackend(
    DockerBackendOptions options,
    TimeProvider? clock = null,
    HttpMessageHandler? handler = null,
    ILogger? logger = null) : IJobBackend, IDisposable
{
    const int StopTimeoutSeconds = 5;
    static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    readonly DockerEngineClient docker = new(options.Endpoint, handler);
    readonly TimeProvider clock = clock ?? TimeProvider.System;
    readonly ILogger logger = logger ?? NullLogger.Instance;

    public async Task<int> RunAsync(JobSpec job, IJobOutput output, CancellationToken cancellationToken)
    {
        if (job.Sandboxed)
        {
            // Never run a job that asked for a sandbox without one.
            throw new NotSupportedException("Sandboxed jobs need the Kubernetes backend with Runner:Kubernetes:Sandbox:Enabled.");
        }

        if (!await docker.ImageExistsAsync(options.JobImage, cancellationToken))
        {
            await docker.PullImageAsync(options.JobImage, cancellationToken);
        }

        // Not cancellable: abandoning the request mid-flight could leave a container we never learn the ID of.
        // A cancellation that arrives meanwhile is honoured inside the try, so the container is still removed.
        string id;
        using (var create = new CancellationTokenSource(CleanupTimeout))
        {
            id = await docker.CreateContainerAsync(CreateRequest(job), create.Token);
        }

        HttpResponseMessage? attach = null;
        Task? pump = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            attach = await docker.AttachAsync(id, cancellationToken);
            await docker.StartAsync(id, cancellationToken);
            output.Started(clock.GetUtcNow());

            // Read only once Started has been reported; anything the job wrote meanwhile waits in the socket.
            var stream = await attach.Content.ReadAsStreamAsync(cancellationToken);
            pump = MultiplexedStreamReader.CopyAsync(stream, output, cancellationToken);

            var exitCode = await docker.WaitAsync(id, cancellationToken);

            // The stream ends when the container exits; drain it so no output arrives after we return.
            await pump;
            return exitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Give the job a moment to exit cleanly. Best effort: the forced removal below kills it regardless.
            using var cleanup = new CancellationTokenSource(CleanupTimeout);
            try
            {
                await docker.StopAsync(id, StopTimeoutSeconds, cleanup.Token);
            }
            catch (Exception e) when (e is InvalidOperationException or HttpRequestException or OperationCanceledException)
            {
            }

            throw;
        }
        finally
        {
            attach?.Dispose();
            if (pump is not null)
            {
                // Disposing the attach response ends the pump if it's still running; it never outlives the run.
                await pump.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            // The caller's token may be cancelled; removal must still happen. A failed removal is logged, never
            // thrown: it must not replace the job's real outcome (its exit code, error or cancellation).
            using var cleanup = new CancellationTokenSource(CleanupTimeout);
            try
            {
                await docker.RemoveAsync(id, cleanup.Token);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to remove container {ContainerId} for job {JobId}", id, job.JobId);
            }
        }
    }

    public void Dispose() => docker.Dispose();

    ContainerCreateRequest CreateRequest(JobSpec job) => new()
    {
        Image = options.JobImage,
        Cmd = ["sh", "-c", job.Command],
        User = options.User,
        Labels = new()
        {
            ["octopus.job-id"] = job.JobId,
            ["octopus.run-id"] = job.RunId,
        },
        HostConfig = new HostConfig
        {
            NetworkMode = "none",
            Memory = options.MemoryLimitBytes,
            NanoCpus = (long)Math.Round(options.CpuLimit * 1_000_000_000),
            ReadonlyRootfs = options.ReadOnlyRootFilesystem,
            CapDrop = ["ALL"],
            SecurityOpt = ["no-new-privileges"],
        },
    };
}
