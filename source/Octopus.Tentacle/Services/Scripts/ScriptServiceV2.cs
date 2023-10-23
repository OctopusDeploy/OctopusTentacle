﻿using System;
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
        readonly ConcurrentDictionary<ScriptTicket, RunningScriptWrapper> runningScripts = new();

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

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request)
        {
            runningScripts.TryGetValue(request.Ticket, out var runningScript);
            return GetResponse(request.Ticket, request.LastLogSequence, runningScript?.Process);
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command)
        {
            if (runningScripts.TryGetValue(command.Ticket, out var runningScript))
            {
                runningScript.Cancel();
            }

            return GetResponse(command.Ticket, command.LastLogSequence, runningScript?.Process);
        }

        public void CompleteScript(CompleteScriptCommandV2 command)
        {
            if (runningScripts.TryRemove(command.Ticket, out var runningScript))
            {
                runningScript.Dispose();
            }

            var workspace = workspaceFactory.GetWorkspace(command.Ticket);
            workspace.Delete();
        }

        RunningScript LaunchShell(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, IScriptStateStore stateStore, CancellationToken cancellationToken)
        {
            var runningScript = new RunningScript(shell, workspace, stateStore, workspace.CreateLog(), serverTaskId, cancellationToken, log);
            var thread = new Thread(runningScript.Execute) { Name = "Executing PowerShell runningScript for " + ticket.TaskId };
            thread.Start();
            return runningScript;
        }

        ScriptStatusResponseV2 GetResponse(ScriptTicket ticket, long lastLogSequence, RunningScript? runningScript)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket);
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
            readonly CancellationTokenSource cancellationTokenSource = new ();

            public RunningScriptWrapper(ScriptStateStore scriptStateStore)
            {
                ScriptStateStore = scriptStateStore;

                CancellationToken = cancellationTokenSource.Token;
            }

            public RunningScript? Process { get; set; }
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
