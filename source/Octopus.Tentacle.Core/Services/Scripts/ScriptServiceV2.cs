using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Maintenance;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Core.Services.Scripts.PowerShellStartup;
using Octopus.Tentacle.Core.Services.Scripts.Shell;
using Octopus.Tentacle.Core.Services.Scripts.StateStore;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Services.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Core.Services.Scripts
{
    [Service(typeof(IScriptServiceV2))]
    public class ScriptServiceV2 : IAsyncScriptServiceV2, IRunningScriptReporter
    {
        readonly IShell shell;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IScriptStateStoreFactory scriptStateStoreFactory;
        readonly ISystemLog log;
        readonly ScriptIsolationMutex scriptIsolationMutex;
        readonly ConcurrentDictionary<ScriptTicket, RunningScriptWrapper> runningScripts = new();
        readonly IReadOnlyDictionary<string, string> environmentVariables;
        readonly TimeSpan powerShellStartupTimeout;

        public ScriptServiceV2(
            IShell shell,
            IScriptWorkspaceFactory workspaceFactory,
            IScriptStateStoreFactory scriptStateStoreFactory,
            ScriptIsolationMutex scriptIsolationMutex,
            ISystemLog log, 
            IReadOnlyDictionary<string, string> environmentVariables,
            TimeSpan powerShellStartupTimeout)
        {
            this.shell = shell;
            this.workspaceFactory = workspaceFactory;
            this.scriptStateStoreFactory = scriptStateStoreFactory;
            this.log = log;
            this.environmentVariables = environmentVariables;
            this.powerShellStartupTimeout = powerShellStartupTimeout;
            this.scriptIsolationMutex = scriptIsolationMutex;
        }

        public ScriptServiceV2(
            IShell shell,
            IScriptWorkspaceFactory workspaceFactory,
            IScriptStateStoreFactory scriptStateStoreFactory,
            ScriptIsolationMutex scriptIsolationMutex,
            ISystemLog log) : this(shell, workspaceFactory, scriptStateStoreFactory, scriptIsolationMutex, log, new Dictionary<string, string>(), PowerShellStartupDetection.PowerShellStartupTimeout)
        {
        }

        public async Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, CancellationToken cancellationToken)
        {
            var runningScript = runningScripts.GetOrAdd(
                command.ScriptTicket,
                _ =>
                {
                    var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket, WorkspaceReadinessCheck.Perform);
                    var scriptState = scriptStateStoreFactory.Create(workspace);
                    return new RunningScriptWrapper(scriptState);
                });

            using (runningScript.StartScriptMutex.Lock())
            {
                IScriptWorkspace workspace;

                // If the state already exists then this runningScript is already running/has already run and we should not run it again.
                // StartScript may be called multiple times for the same ticket (e.g. server retries), so we guard against double-launching.
                if (runningScript.ScriptStateStore.Exists())
                {
                    var state = runningScript.ScriptStateStore.Load();

                    if (state.HasStarted() || runningScript.Process != null)
                    {
                        return GetResponse(command.ScriptTicket, 0, runningScript.Process);
                    }

                    workspace = workspaceFactory.GetWorkspace(command.ScriptTicket, WorkspaceReadinessCheck.Perform);
                }
                else
                {
                    workspace = await workspaceFactory.PrepareWorkspace(command.ScriptTicket,
                        command.ScriptBody,
                        command.Scripts,
                        command.Isolation,
                        command.ScriptIsolationMutexTimeout,
                        command.IsolationMutexName,
                        command.Arguments,
                        command.Files,
                        CancellationToken.None);

                    runningScript.ScriptStateStore.Create();
                }

                var process = LaunchShell(command.ScriptTicket,
                    command.TaskId,
                    workspace,
                    runningScript.ScriptStateStore,
                    runningScript.CancellationToken,
                    runningScript.AbandonToken);

                runningScript.Process = process;

                if (command.DurationToWaitForScriptToFinish != null)
                {
                    var waited = Stopwatch.StartNew();
                    while (process.State != ProcessState.Complete && waited.Elapsed < command.DurationToWaitForScriptToFinish.Value)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    }
                }

                return GetResponse(command.ScriptTicket, 0, runningScript.Process);
            }
        }

        public async Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            runningScripts.TryGetValue(request.Ticket, out var runningScript);
            return GetResponse(request.Ticket, request.LastLogSequence, runningScript?.Process);
        }

        public async Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            if (runningScripts.TryGetValue(command.Ticket, out var runningScript))
            {
                runningScript.Cancel();
            }

            return GetResponse(command.Ticket, command.LastLogSequence, runningScript?.Process);
        }

        public async Task<ScriptStatusResponseV2> AbandonScriptAsync(AbandonScriptCommandV2 command, CancellationToken cancellationToken)
        {
            if (runningScripts.TryGetValue(command.Ticket, out var runningScript))
            {
                runningScript.Abandon();

                // Wait briefly for Execute() to react to the abandon token and reach Complete state.
                // The spec promises that AbandonScript returns (Complete, AbandonedExitCode) immediately.
                // Without this wait, callers see Running state because they ran ahead of Execute()'s
                // post-await unwinding.
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline
                       && runningScript.Process?.State != ProcessState.Complete
                       && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                }
            }

            return GetResponse(command.Ticket, command.LastLogSequence, runningScript?.Process);
        }

        public async Task CompleteScriptAsync(CompleteScriptCommandV2 command, CancellationToken cancellationToken)
        {
            if (runningScripts.TryRemove(command.Ticket, out var runningScript))
            {
                runningScript.Dispose();
            }

            var workspace = workspaceFactory.GetWorkspace(command.Ticket, WorkspaceReadinessCheck.Skip);

            var stateStore = scriptStateStoreFactory.Create(workspace);
            var wasAbandoned = stateStore.Exists()
                               && stateStore.Load().ExitCode == ScriptExitCodes.AbandonedExitCode;

            if (wasAbandoned)
            {
                try
                {
                    await workspace.Delete(cancellationToken);
                }
                catch (Exception ex)
                {
                    log.Warn(ex, $"Could not delete abandoned workspace at {workspace.WorkingDirectory}. Leaving on disk; the underlying script process may still hold open file handles.");
                }
            }
            else
            {
                await workspace.Delete(cancellationToken);
            }
        }

        RunningScript LaunchShell(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, IScriptStateStore stateStore, CancellationToken cancellationToken, CancellationToken abandonToken)
        {
            var runningScript = new RunningScript(shell, workspace, stateStore, workspace.CreateLog(), serverTaskId, scriptIsolationMutex, cancellationToken, abandonToken, environmentVariables, powerShellStartupTimeout, log);
            _ = Task.Run(async () => await runningScript.Execute());
            return runningScript;
        }

        ScriptStatusResponseV2 GetResponse(ScriptTicket ticket, long lastLogSequence, RunningScript? runningScript)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket, WorkspaceReadinessCheck.Skip);
            var scriptLog = runningScript?.ScriptLog ?? workspace.CreateLog();
            var logs = scriptLog.GetOutput(lastLogSequence, out var next);

            if (runningScript != null)
            {
                return new ScriptStatusResponseV2(ticket, runningScript.State, runningScript.ExitCode, logs, next);
            }

            // If we don't have a RunningProcess we check the ScriptStateStore to see if we have persisted a script result
            var scriptStateStore = scriptStateStoreFactory.Create(workspace);
            if (scriptStateStore.Exists())
            {
                var scriptState = scriptStateStore.Load();

                if (!scriptState.HasCompleted())
                {
                    scriptState.Complete(ScriptExitCodes.UnknownResultExitCode, false);
                    scriptStateStore.Save(scriptState);
                }

                return new ScriptStatusResponseV2(ticket, scriptState.State, scriptState.ExitCode ?? ScriptExitCodes.UnknownResultExitCode, logs, next);
            }

            return new ScriptStatusResponseV2(ticket, ProcessState.Complete, ScriptExitCodes.UnknownScriptExitCode, logs, next);
        }

        public bool IsRunningScript(ScriptTicket ticket)
        {
            if (runningScripts.TryGetValue(ticket, out var script))
            {
                if (script.Process?.State != ProcessState.Complete)
                {
                    return true;
                }
            }

            return false;
        }

        class RunningScriptWrapper : IDisposable
        {
            readonly CancellationTokenSource cancellationTokenSource = new();
            readonly CancellationTokenSource abandonTokenSource = new();

            public RunningScriptWrapper(ScriptStateStore scriptStateStore)
            {
                ScriptStateStore = scriptStateStore;
                CancellationToken = cancellationTokenSource.Token;
                AbandonToken = abandonTokenSource.Token;
            }

            public RunningScript? Process { get; set; }
            public ScriptStateStore ScriptStateStore { get; }
            public SemaphoreSlim StartScriptMutex { get; } = new(1, 1);

            public CancellationToken CancellationToken { get; }
            public CancellationToken AbandonToken { get; }

            public void Cancel() => cancellationTokenSource.Cancel();
            public void Abandon() => abandonTokenSource.Cancel();

            public void Dispose()
            {
                cancellationTokenSource.Dispose();
                abandonTokenSource.Dispose();
            }
        }
    }
}
