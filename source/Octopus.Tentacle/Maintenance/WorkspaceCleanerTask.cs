using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Maintenance
{
    public interface IWorkspaceCleanerTask
    {
        void Start();
        void Stop();
    }

    class WorkspaceCleanerTask : IWorkspaceCleanerTask, IDisposable
    {
        readonly WorkspaceCleanerConfiguration configuration;
        readonly WorkspaceCleaner workspaceCleaner;
        readonly ISystemLog log;

        readonly CancellationTokenSource cancellationTokenSource = new ();
        readonly object taskLock = new();

        Task? cleanerTask;

        public WorkspaceCleanerTask(
            WorkspaceCleanerConfiguration configuration,
            WorkspaceCleaner workspaceCleaner, 
            ISystemLog log)
        {
            this.configuration = configuration;
            this.workspaceCleaner = workspaceCleaner;
            this.log = log;
        }

        public void Dispose()
        {
            Stop();

            cancellationTokenSource.Dispose();
        }

        public void Start()
        {
            lock (taskLock)
            {
                cleanerTask = Task.Run(RunTask);
            }
        }

        public void Stop()
        {
            lock (taskLock)
            {
                if (cleanerTask is null) return;

                try
                {
                    cancellationTokenSource.Cancel();

                    cleanerTask.Wait();
                }
                catch (Exception e)
                {
                    log.Error(e, "Could not stop workspace cleaner");
                }
                finally
                {
                    cleanerTask = null;
                }
            }
        }

        async Task RunTask()
        {
            var cancellationToken = cancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await workspaceCleaner.Clean(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // This is just the application closing. Ignore.
                }
                catch (Exception e)
                {
                    log.Error(e, "Error running workspace cleaner");
                }

                try
                {
                    // Do the delay separately, otherwise a failure in `Clean` could result in this code spinning wheels.
                    await Task.Delay(configuration.CleaningDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // This is just the application closing. Ignore.
                }
            }
        }
    }
}