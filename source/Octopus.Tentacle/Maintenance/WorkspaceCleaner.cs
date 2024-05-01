using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Time;

namespace Octopus.Tentacle.Maintenance
{
    public class WorkspaceCleaner
    {
        readonly WorkspaceCleanerConfiguration configuration;
        readonly IScriptWorkspaceFactory scriptWorkspaceFactory;
        readonly IEnumerable<IRunningScriptReporter> runningScriptReporters;
        readonly IClock clock;
        readonly ISystemLog log;

        public WorkspaceCleaner(
            WorkspaceCleanerConfiguration configuration,
            IScriptWorkspaceFactory scriptWorkspaceFactory,
            IEnumerable<IRunningScriptReporter> runningScriptReporters,
            IClock clock,
            ISystemLog log)
        {
            this.configuration = configuration;
            this.scriptWorkspaceFactory = scriptWorkspaceFactory;
            this.runningScriptReporters = runningScriptReporters;
            this.clock = clock;
            this.log = log;
        }

        public async Task Clean(CancellationToken cancellationToken)
        {
            var deleteWorkspacesOlderThanDateTimeUtc = clock.GetUtcTime().DateTime.Subtract(configuration.DeleteWorkspacesOlderThanTimeSpan);
            log.Verbose($"Cleaning workspaces older than {deleteWorkspacesOlderThanDateTimeUtc:g}");

            var deletedCount = 0;
            var workspaces = scriptWorkspaceFactory.GetUncompletedWorkspaces();
            foreach (var workspace in workspaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (runningScriptReporters.Any(x => x.IsRunningScript(workspace.ScriptTicket)))
                    {
                        continue;
                    }

                    if(!DoWorkspaceFilesLookOldEnoughToDelete(workspace, deleteWorkspacesOlderThanDateTimeUtc))
                    {
                        continue;
                    }

                    await workspace.Delete(cancellationToken);
                    deletedCount++;
                }
                catch (Exception e)
                {
                    log.Warn(e, $"Could not delete workspace {workspace.WorkingDirectory}.");
                }
            }

            if (deletedCount > 0)
            {
                log.Info($"Deleted {deletedCount} workspace{(deletedCount != 1 ? "s" : "")}.");
            }
            else
            {
                log.Verbose("No workspaces found that need to be deleted.");
            }
        }

        static bool DoWorkspaceFilesLookOldEnoughToDelete(IScriptWorkspace workspace, DateTime deleteWorkspacesOlderThanDateTimeUtc)
        {
            
            return
                //First look to see if there is a log file and if we can delete the workspace based on that
                IsWorkspaceFileOlderThanCheckTime(workspace.LogFilePath, deleteWorkspacesOlderThanDateTimeUtc)
                // Fall back to just the boostrap script file, in k8s agent a log file is never created.
                ?? IsWorkspaceFileOlderThanCheckTime(workspace.BootstrapScriptFilePath, deleteWorkspacesOlderThanDateTimeUtc)
                ?? false;
        }

        static bool? IsWorkspaceFileOlderThanCheckTime(string filePath, DateTime checkTime)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }
            
            //find the last time the file was written and see if that is more than 
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
            if (lastWriteTimeUtc >= checkTime)
            {
                return false;
            }

            return true;
        }
    }
}