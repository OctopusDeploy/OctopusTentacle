using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Retries;
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

        public async Task<bool> Stop(CancellationToken cancellationToken)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            if (runningTentacleTask != null)
            {
                var task = runningTentacleTask;
                runningTentacleTask = null;

                return await task.WaitTillCompletedOrCancelled(cancellationToken);
            }

            return true;
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
            
            logger.Information("Starting Stop");
            var stopped = await Stop(cancellationToken);
            if (!stopped)
            {
                logger.Warning("Tentacle did not stop in time and may still be running");
            }

            logger.Information("Starting deleteInstanceFunction");
            await deleteInstanceFunction(cancellationToken);

            logger.Information("Starting temporaryDirectory.Dispose");
            temporaryDirectory.Dispose();

            logger.Information("Finished DisposeAsync");
        }
    }
}