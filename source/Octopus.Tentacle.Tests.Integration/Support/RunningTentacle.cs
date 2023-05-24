using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class RunningTentacle : IDisposable
    {
        private readonly IDisposable temporaryDirectory;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? runningTentacleTask;
        private readonly Func<CancellationToken, Task<(Task runningTentacleTask, Uri serviceUri)>> startTentacleFunction;

        public RunningTentacle(
            IDisposable temporaryDirectory,
            Func<CancellationToken, Task<(Task, Uri)>> startTentacleFunction,
            string thumbprint)
        {
            this.startTentacleFunction = startTentacleFunction;
            this.temporaryDirectory = temporaryDirectory;

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

            cancellationTokenSource = new CancellationTokenSource();

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

        public void Dispose()
        {
            if (runningTentacleTask != null)
            {
                Stop(CancellationToken.None).GetAwaiter().GetResult();
            }

            temporaryDirectory.Dispose();
        }
    }
}