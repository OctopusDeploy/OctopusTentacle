using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class RunningTentacle : IDisposable
    {
        private readonly IDisposable temporaryDirectory;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? runningTentacleTask;
        private readonly Func<CancellationToken, Task<(Task runningTentacleTask, Uri serviceUri)>> startTentacleFunction;
        private ILogger logger;

        public RunningTentacle(
            IDisposable temporaryDirectory,
            Func<CancellationToken, Task<(Task, Uri)>> startTentacleFunction,
            string thumbprint)
        {
            this.startTentacleFunction = startTentacleFunction;
            this.temporaryDirectory = temporaryDirectory;
            this.logger = new SerilogLoggerBuilder().Build().ForContext<RunningTentacle>();

            Thumbprint = thumbprint;
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

            var t = runningTentacleTask;
            runningTentacleTask = null;
            await t;
        }

        private void StopOnDispose(CancellationToken cancellationToken)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            var t = runningTentacleTask;
            runningTentacleTask = null;

            var stopDuration = Stopwatch.StartNew();
            while (t?.IsCompleted == false && stopDuration.Elapsed < TimeSpan.FromSeconds(10))
            {
                Thread.Sleep(10);
            }

            if (t?.IsCompleted == false)
            {
                logger.Warning("Failed to stop Running Tentacle");
            }
        }

        public async Task Restart(CancellationToken cancellationToken)
        {
            await Stop(cancellationToken);
            await Start(cancellationToken);
        }

        public void Dispose()
        {
            if (runningTentacleTask != null)
            {
                //StopOnDispose(CancellationToken.None);
                Stop(CancellationToken.None).GetAwaiter().GetResult();
            }

            temporaryDirectory.Dispose();
        }
    }
}