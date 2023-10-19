using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha
{
    public class ShellScriptServiceV3AlphaExecutor : IScriptServiceV3AlphaExecutor
    {
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IShell shell;
        readonly IScriptStateStoreFactory scriptStateStoreFactory;
        readonly ISystemLog log;
        readonly ConcurrentDictionary<ScriptTicket, RunningScriptWrapper> runningScripts = new();

        public ShellScriptServiceV3AlphaExecutor(
            IShell shell,
            IScriptWorkspaceFactory workspaceFactory,
            IScriptStateStoreFactory scriptStateStoreFactory,
            ISystemLog log)
        {
            this.shell = shell;
            this.workspaceFactory = workspaceFactory;
            this.scriptStateStoreFactory = scriptStateStoreFactory;
            this.log = log;
        }

        public bool ValidateExecutionContext(IScriptExecutionContext executionContext)
            => executionContext is LocalShellScriptExecutionContext;

        public async Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            var runningScript = runningScripts.GetOrAdd(
                command.ScriptTicket,
                _ =>
                {
                    var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
                    var scriptState = scriptStateStoreFactory.Create(workspace);
                    return new RunningScriptWrapper(scriptState);
                });

            using (runningScript.StartScriptMutex.Lock())
            {
                IScriptWorkspace workspace;

                // If the state already exists then this runningScript is already running/has already run and we should not run it again
                if (runningScript.ScriptStateStore.Exists())
                {
                    var state = runningScript.ScriptStateStore.Load();

                    if (state.HasStarted() || runningScript.Process != null)
                    {
                        return GetResponse(command.ScriptTicket, 0, runningScript.Process);
                    }

                    workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
                }
                else
                {
                    workspace = workspaceFactory.PrepareWorkspace(command.ScriptTicket,
                        command.ScriptBody,
                        command.Scripts,
                        command.Isolation,
                        command.ScriptIsolationMutexTimeout,
                        command.IsolationMutexName,
                        command.Arguments,
                        command.Files);

                    runningScript.ScriptStateStore.Create();
                }

                var process = LaunchShell(command.ScriptTicket, command.TaskId, workspace, runningScript.ScriptStateStore, runningScript.CancellationToken);

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

        RunningShellScript LaunchShell(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, IScriptStateStore stateStore, CancellationToken cancellationToken)
        {
            var runningShellScript = new RunningShellScript(shell, workspace, stateStore, workspace.CreateLog(), serverTaskId, cancellationToken, log);
            var thread = new Thread(runningShellScript.Execute) { Name = "Executing PowerShell runningShellScript for " + ticket.TaskId };
            thread.Start();
            return runningShellScript;
        }

        public async Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            runningScripts.TryGetValue(request.Ticket, out var runningScript);
            return GetResponse(request.Ticket, request.LastLogSequence, runningScript?.Process);
        }

        public async Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (runningScripts.TryGetValue(command.Ticket, out var runningScript))
            {
                runningScript.Cancel();
            }

            return GetResponse(command.Ticket, command.LastLogSequence, runningScript?.Process);
        }

        public async Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (runningScripts.TryRemove(command.Ticket, out var runningScript))
            {
                runningScript.Dispose();
            }

            var workspace = workspaceFactory.GetWorkspace(command.Ticket);
            workspace.Delete();
        }
        ScriptStatusResponseV3Alpha GetResponse(ScriptTicket ticket, long lastLogSequence, IRunningScript? runningScript)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket);
            var scriptLog = runningScript?.ScriptLog ?? workspace.CreateLog();
            var logs = scriptLog.GetOutput(lastLogSequence, out var next);

            if (runningScript != null)
            {
                return new ScriptStatusResponseV3Alpha(ticket, runningScript.State, runningScript.ExitCode, logs, next);
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

                return new ScriptStatusResponseV3Alpha(ticket, scriptState.State, scriptState.ExitCode ?? ScriptExitCodes.UnknownResultExitCode, logs, next);
            }

            return new ScriptStatusResponseV3Alpha(ticket, ProcessState.Complete, ScriptExitCodes.UnknownScriptExitCode, logs, next);
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

            public RunningScriptWrapper(ScriptStateStore scriptStateStore)
            {
                ScriptStateStore = scriptStateStore;

                CancellationToken = cancellationTokenSource.Token;
            }

            public IRunningScript? Process { get; set; }
            public ScriptStateStore ScriptStateStore { get; }
            public SemaphoreSlim StartScriptMutex { get; } = new(1, 1);

            public CancellationToken CancellationToken { get; }

            public void Cancel()
            {
                cancellationTokenSource.Cancel();
            }

            public void Dispose()
            {
                cancellationTokenSource.Dispose();
            }
        }
    }
}