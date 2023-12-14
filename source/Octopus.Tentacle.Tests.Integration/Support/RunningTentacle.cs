using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Retries;
using Polly;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class RunningTentacle : IAsyncDisposable
    {
        private readonly TemporaryDirectory temporaryDirectory;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? runningTentacleTask;
        private readonly Func<CancellationToken, Task<(Task runningTentacleTask, Uri serviceUri)>> startTentacleFunction;
        private readonly Func<CancellationToken, Task> deleteInstanceFunction;
        private ILogger logger;

        public RunningTentacle(FileInfo tentacleExe,
            TemporaryDirectory temporaryDirectory,
            Func<CancellationToken, Task<(Task, Uri)>> startTentacleFunction,
            string thumbprint,
            string instanceName,
            string homeDirectory,
            string applicationDirectory,
            Func<CancellationToken, Task> deleteInstanceFunction,
            Dictionary<string, string> runTentacleEnvironmentVariables,
            ILogger logger)
        {
            this.startTentacleFunction = startTentacleFunction;
            this.temporaryDirectory = temporaryDirectory;
            this.deleteInstanceFunction = deleteInstanceFunction;

            TentacleExe = tentacleExe;
            Thumbprint = thumbprint;
            InstanceName = instanceName;
            HomeDirectory = homeDirectory;
            ApplicationDirectory = applicationDirectory;
            RunTentacleEnvironmentVariables = runTentacleEnvironmentVariables;

            this.logger = logger.ForContext<RunningTentacle>();
        }

        public Uri ServiceUri { get; private set; }
        public string InstanceName { get; }
        public string Thumbprint { get; }
        public string HomeDirectory { get; }
        public string ApplicationDirectory { get; }
        public IReadOnlyDictionary<string, string> RunTentacleEnvironmentVariables { get; }
        public FileInfo TentacleExe { get; }
        public string LogFilePath => Path.Combine(HomeDirectory, "Logs", "OctopusTentacle.txt");

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

        public string ReadAllLogFileText()
        {
            var content = Policy
                .Handle<IOException>()
                .WaitAndRetry(
                    10,
                    retryCount => TimeSpan.FromMilliseconds(100 * retryCount),
                    (exception, _) => { logger.Information("Failed to read file {File}: {Message}. Retrying!", LogFilePath, exception.Message); })
                .Execute(() => File.ReadAllText(LogFilePath));

            return content;
        }
    }
}