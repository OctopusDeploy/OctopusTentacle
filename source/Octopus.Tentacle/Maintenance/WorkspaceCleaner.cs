﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Services.Scripts;
using Octopus.Time;

namespace Octopus.Tentacle.Maintenance
{
    public class WorkspaceCleaner
    {
        readonly WorkspaceCleanerConfiguration configuration;
        readonly IScriptWorkspaceFactory scriptWorkspaceFactory;
        readonly IClock clock;
        readonly ISystemLog log;

        readonly ScriptService scriptService;
        readonly ScriptServiceV2 scriptServiceV2;

        public WorkspaceCleaner(
            WorkspaceCleanerConfiguration configuration,
            IScriptWorkspaceFactory scriptWorkspaceFactory,
            IServiceRegistration serviceRegistration,
            IClock clock,
            ISystemLog log)
        {
            this.configuration = configuration;
            this.scriptWorkspaceFactory = scriptWorkspaceFactory;
            this.clock = clock;
            this.log = log;

            scriptService = serviceRegistration.GetService<ScriptService>();
            scriptServiceV2 = serviceRegistration.GetService<ScriptServiceV2>();
        }

        public async Task Clean(CancellationToken cancellationToken)
        {
            var deleteWorkspacesOlderThanDateTimeUtc = clock.GetUtcTime().DateTime.Subtract(configuration.DeleteWorkspacesOlderThanTimeSpan);
            log.Verbose($"Cleaning orphaned workspaces older than {deleteWorkspacesOlderThanDateTimeUtc:g}");

            var deletedCount = 0;
            var workspaces = scriptWorkspaceFactory.GetUncompletedWorkspaces();
            foreach (var workspace in workspaces)
            {
                try
                {
                    if (scriptService.IsRunningScript(workspace.ScriptTicket)) continue;
                    if (scriptServiceV2.IsRunningScript(workspace.ScriptTicket)) continue;

                    var workspaceLogFilePath = workspace.LogFilePath;
                    if (!File.Exists(workspaceLogFilePath)) continue;

                    var outputLogFileLastWriteTimeUtc = File.GetLastWriteTimeUtc(workspaceLogFilePath);
                    if (outputLogFileLastWriteTimeUtc >= deleteWorkspacesOlderThanDateTimeUtc) continue;

                    await workspace.Delete(cancellationToken);
                    deletedCount++;
                }
                catch (Exception e)
                {
                    log.Error(e, $"Could not delete orphaned workspace {workspace.WorkingDirectory}.");
                }
            }

            if (deletedCount > 0)
            {
                log.Info($"Deleted {deletedCount} orphaned workspace{(deletedCount != 1 ? "s" : "")}.");
            }
            else
            {
                log.Verbose("No orphaned workspaces found.");
            }
        }
    }
}