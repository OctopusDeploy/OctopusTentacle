using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.Scripts
{
    [Service(typeof(IScriptServiceV2))]
    public class ScriptServiceV2 : IScriptServiceV2
    {
        readonly IShell shell;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IScriptStateStoreFactory scriptStateStoreFactory;
        readonly ISystemLog log;
        readonly ConcurrentDictionary<ScriptTicket, RunningShellScriptWrapper> runningShellScripts = new();

        public ScriptServiceV2(
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

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command)
        {
            var runningShellScript = runningShellScripts.GetOrAdd(
                command.ScriptTicket,
                _ =>
                {
                    var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
                    var scriptState = scriptStateStoreFactory.Create(workspace);
                    return new RunningShellScriptWrapper(scriptState);
                });

            using (runningShellScript.StartScriptMutex.Lock())
            {
                IScriptWorkspace workspace;

                // If the state already exists then this runningShellScript is already running/has already run and we should not run it again
                if (runningShellScript.ScriptStateStore.Exists())
                {
                    var state = runningShellScript.ScriptStateStore.Load();

                    if (state.HasStarted() || runningShellScript.Process != null)
                    {
                        return GetResponse(command.ScriptTicket, 0, runningShellScript.Process);
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

                    runningShellScript.ScriptStateStore.Create();
                }

                var process = LaunchShell(command.ScriptTicket, command.TaskId, workspace, runningShellScript.ScriptStateStore, runningShellScript.CancellationToken);

                runningShellScript.Process = process;

                if (command.DurationToWaitForScriptToFinish != null)
                {
                    var waited = Stopwatch.StartNew();
                    while (process.State != ProcessState.Complete && waited.Elapsed < command.DurationToWaitForScriptToFinish.Value)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    }
                }

                return GetResponse(command.ScriptTicket, 0, runningShellScript.Process);
            }
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
        {
            runningShellScripts.TryGetValue(request.Ticket, out var runningShellScript);
            return GetResponse(request.Ticket, request.LastLogSequence, runningShellScript?.Process);
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
        {
            if (runningShellScripts.TryGetValue(command.Ticket, out var runningShellScript))
            {
                runningShellScript.Cancel();
            }

            return GetResponse(command.Ticket, command.LastLogSequence, runningShellScript?.Process);
        }

        public void CompleteScript(CompleteScriptCommandV2 command)
        {
            if (runningShellScripts.TryRemove(command.Ticket, out var runningShellScript))
            {
                runningShellScript.Dispose();
            }

            var workspace = workspaceFactory.GetWorkspace(command.Ticket);
            workspace.Delete();
        }

        RunningShellScript LaunchShell(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, IScriptStateStore stateStore, CancellationToken cancellationToken)
        {
            var runningShellScript = new RunningShellScript(shell, workspace, stateStore, workspace.CreateLog(), serverTaskId, cancellationToken, log);
            var thread = new Thread(runningShellScript.Execute) { Name = "Executing PowerShell runningShellScript for " + ticket.TaskId };
            thread.Start();
            return runningShellScript;
        }

        ScriptStatusResponseV2 GetResponse(ScriptTicket ticket, long lastLogSequence, RunningShellScript? runningShellScript)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket);
            var scriptLog = runningShellScript?.ScriptLog ?? workspace.CreateLog();
            var logs = scriptLog.GetOutput(lastLogSequence, out var next);

            if (runningShellScript != null)
            {
                return new ScriptStatusResponseV2(ticket, runningShellScript.State, runningShellScript.ExitCode, logs, next);
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
            if (runningShellScripts.TryGetValue(ticket, out var script))
            {
                if (script.Process?.State != ProcessState.Complete)
                {
                    return true;
                }
            }

            return false;
        }

        class RunningShellScriptWrapper : IDisposable
        {
            readonly CancellationTokenSource cancellationTokenSource = new ();

            public RunningShellScriptWrapper(ScriptStateStore scriptStateStore)
            {
                ScriptStateStore = scriptStateStore;

                CancellationToken = cancellationTokenSource.Token;
            }

            public RunningShellScript? Process { get; set; }
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