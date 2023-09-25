using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class RunningTentacle : IAsyncDisposable
    {
        private readonly IDisposable temporaryDirectory;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? runningTentacleTask;
        private readonly Func<CancellationToken, Task<(Task runningTentacleTask, Uri serviceUri)>> startTentacleFunction;
        private readonly Func<CancellationToken, Task> deleteInstanceFunction;
        private ILogger logger;

        public RunningTentacle(
            IDisposable temporaryDirectory,
            Func<CancellationToken, Task<(Task, Uri)>> startTentacleFunction,
            string thumbprint, 
            Func<CancellationToken, Task> deleteInstanceFunction,
            ILogger logger)
        {
            this.startTentacleFunction = startTentacleFunction;
            this.temporaryDirectory = temporaryDirectory;
            this.logger = logger.ForContext<RunningTentacle>();

            Thumbprint = thumbprint;
            this.deleteInstanceFunction = deleteInstanceFunction;
        }

        public Uri ServiceUri { get; private set; }
        public string Thumbprint { get; }

        public async Task Start(CancellationToken cancellationToken)
        {
            if (runningTentacleTask != null)
            {
                throw new Exception("Tentacle is already running, call stop() first");
            }

            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var (rtt, serviceUri) = await startTentacleFunction(cancellationTokenSource.Token);

            runningTentacleTask = rtt;
            ServiceUri = serviceUri;
        }

        public async Task Stop(CancellationToken cancellationToken)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            var task = runningTentacleTask;
            runningTentacleTask = null;
            await task;
        }

        private async Task StopOnDispose(CancellationToken cancellationToken)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            var task = runningTentacleTask;
            runningTentacleTask = null;

            var stopDuration = Stopwatch.StartNew();
            while (task?.IsCompleted == false && stopDuration.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }

            if (task?.IsCompleted == false)
            {
                logger.Warning("Failed to stop Running Tentacle");
            }
        }

        public async Task Restart(CancellationToken cancellationToken)
        {
            await Stop(cancellationToken);
            await Start(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            using var disposeCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var cancellationToken = disposeCancellationTokenSource.Token;
            logger.Information("Starting DisposeAsync");
            if (runningTentacleTask != null)
            {
                logger.Information("Starting StopOnDispose");
                await StopOnDispose(cancellationToken);
            }

            logger.Information("Starting deleteInstanceFunction");
            await deleteInstanceFunction(cancellationToken);

            logger.Information("Starting temporaryDirectory.Dispose");
            temporaryDirectory.Dispose();

            logger.Information("Finished DisposeAsync");
        }
    }
}