using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Background;

namespace Octopus.Tentacle.Maintenance
{
    public interface IWorkspaceCleanerTask : IBackgroundTask
    {}

    class WorkspaceCleanerTask : BackgroundTask, IWorkspaceCleanerTask
    {
        readonly WorkspaceCleanerConfiguration configuration;
        readonly WorkspaceCleaner workspaceCleaner;

        public WorkspaceCleanerTask(
            WorkspaceCleanerConfiguration configuration,
            WorkspaceCleaner workspaceCleaner, 
            ISystemLog log) : base(log, TimeSpan.FromSeconds(30))
        {
            this.configuration = configuration;
            this.workspaceCleaner = workspaceCleaner;
        }

        protected override async Task RunTask(CancellationToken cancellationToken)
        {
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
                    log.Error(e, "WorkspaceCleanerTask.RunTask:");
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