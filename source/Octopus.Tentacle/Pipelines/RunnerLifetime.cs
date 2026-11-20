#if PIPELINES_RUNNER
#nullable enable
using System.Threading;
using Microsoft.Extensions.Hosting;

namespace Octopus.Tentacle.Pipelines
{
    /// <summary>
    /// The runner's application lifetime inside Tentacle, which has no generic host. Only
    /// <see cref="ApplicationStopping"/> is used: the runner cancels running jobs with it.
    /// It is never disposed, so a job that ignores cancellation can still read the token after Stop().
    /// </summary>
    sealed class RunnerLifetime : IHostApplicationLifetime
    {
        readonly CancellationTokenSource stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => stopping.Cancel();
    }
}
#endif
