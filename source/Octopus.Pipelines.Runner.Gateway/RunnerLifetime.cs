using Microsoft.Extensions.Hosting;

namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// An application lifetime for hosts without a generic host (Tentacle, the CLI). Only
/// <see cref="ApplicationStopping"/> is used: <see cref="GatewayJobExecutor"/> cancels running jobs with it.
/// It is never disposed, so a job that ignores cancellation can still read the token after stopping.
/// </summary>
public sealed class RunnerLifetime : IHostApplicationLifetime
{
    readonly CancellationTokenSource stopping = new();

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => stopping.Token;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() => stopping.Cancel();
}
