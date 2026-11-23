using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.CommonTestUtils;
using Polly;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class RunningTentacle : IAsyncDisposable
    {
        private readonly TemporaryDirectory? temporaryDirectoryToDelete;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? runningTentacleTask;
        private readonly Func<TentacleInstance, CancellationToken, Task<(Task runningTentacleTask, Uri serviceUri)>> startTentacleFunction;
        private readonly Func<TentacleInstance, CancellationToken, Task> deleteInstanceFunction;
        private ILogger logger;

        public RunningTentacle(TentacleInstance instance,
            TemporaryDirectory? temporaryDirectoryToDelete,
            Func<TentacleInstance, CancellationToken, Task<(Task, Uri)>> startTentacleFunction,
            string applicationDirectory,
            Func<TentacleInstance, CancellationToken, Task> deleteInstanceFunction,
            IReadOnlyDictionary<string, string> runTentacleEnvironmentVariables,
            ILogger logger)
        {
            this.startTentacleFunction = startTentacleFunction;
            this.temporaryDirectoryToDelete = temporaryDirectoryToDelete;
            this.deleteInstanceFunction = deleteInstanceFunction;

            Instance = instance;
            ApplicationDirectory = applicationDirectory;
            RunTentacleEnvironmentVariables = runTentacleEnvironmentVariables;

            this.logger = logger.ForContext<RunningTentacle>();
        }

        public Uri ServiceUri { get; private set; }
        public TentacleInstance Instance { get; private set; }
        public string InstanceName => Instance.Name!;
        public string Thumbprint => Instance.Thumbprint!;
        public string HomeDirectory => Instance.Home.DirectoryPath;
        public string ApplicationDirectory { get; }
        public IReadOnlyDictionary<string, string> RunTentacleEnvironmentVariables { get; }
        public FileInfo TentacleExe => new(Instance.TentacleExe);
        public string LogFilePath => Path.Combine(HomeDirectory, "Logs", "OctopusTentacle.txt");

        public async Task Start(CancellationToken cancellationToken)
        {
            if (runningTentacleTask != null)
            {
                throw new Exception("Tentacle is already running, call stop() first");
            }

            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var (rtt, serviceUri) = await startTentacleFunction(Instance, cancellationTokenSource.Token);

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

        // As if the current Tentacle had been installed over the running one.
        public async Task RestartWithCurrentTentacle(CancellationToken cancellationToken)
        {
            await Stop(cancellationToken);
            Instance = Instance.WithCurrentTentacleExe();
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
            await deleteInstanceFunction(Instance, cancellationToken);

            logger.Information("Starting temporaryDirectory.Dispose");
            temporaryDirectoryToDelete?.Dispose();

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