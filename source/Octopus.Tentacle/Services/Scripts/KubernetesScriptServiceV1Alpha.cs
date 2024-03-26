using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;
using Octopus.Tentacle.Kubernetes.Scripts;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.Scripts
{
    [KubernetesService(typeof(IKubernetesScriptServiceV1Alpha))]
    public class KubernetesScriptServiceV1Alpha : IAsyncKubernetesScriptServiceV1Alpha
    {
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IScriptStateStoreFactory scriptStateStoreFactory;
        readonly KubernetesPodScriptExecutor executor;
        readonly ConcurrentDictionary<ScriptTicket, RunningScriptWrapper> runningScripts = new();

        public KubernetesScriptServiceV1Alpha(
            IScriptWorkspaceFactory workspaceFactory,
            IScriptStateStoreFactory scriptStateStoreFactory,
            KubernetesPodScriptExecutor executor)
        {
            this.workspaceFactory = workspaceFactory;
            this.scriptStateStoreFactory = scriptStateStoreFactory;
            this.executor = executor;
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> StartScriptAsync(StartKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken)
        {
            var runningScript = runningScripts.GetOrAdd(
                command.ScriptTicket,
                _ =>
                {
                    var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
                    var scriptState = scriptStateStoreFactory.Create(workspace);
                    return new RunningScriptWrapper(scriptState);
                });

            using (await runningScript.StartScriptMutex.LockAsync(cancellationToken))
            {
                IScriptWorkspace workspace;

                // If the state already exists then this runningScript is already running/has already run and we should not run it again
                if (runningScript.ScriptStateStore.Exists())
                {
                    var state = runningScript.ScriptStateStore.Load();

                    if (state.HasStarted() || runningScript.Process != null)
                    {
                        return await GetResponse(command.ScriptTicket, 0, runningScript.Process);
                    }

                    workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
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
                        cancellationToken);

                    runningScript.ScriptStateStore.Create();
                }

                var process = executor.ExecuteOnBackgroundThread(command, workspace, runningScript.ScriptStateStore, runningScript.CancellationToken);

                runningScript.Process = process;

                return await GetResponse(command.ScriptTicket, 0, runningScript.Process);
            }
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> GetStatusAsync(KubernetesScriptStatusRequestV1Alpha request, CancellationToken cancellationToken)
        {
            runningScripts.TryGetValue(request.ScriptTicket, out var runningScript);
            return await GetResponse(request.ScriptTicket, request.LastLogSequence, runningScript?.Process);
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptAsync(CancelKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken)
        {
            if (runningScripts.TryGetValue(command.ScriptTicket, out var runningScript))
            {
                runningScript.Cancel();
            }

            return await GetResponse(command.ScriptTicket, command.LastLogSequence, runningScript?.Process);
        }

        public async Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken)
        {
            if (runningScripts.TryRemove(command.ScriptTicket, out var runningScript))
            {
                runningScript.Dispose();
            }

            var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
            await workspace.Delete(cancellationToken);

            if (runningScript?.Process is not null)
                await runningScript.Process.Cleanup(cancellationToken);
        }

        async Task<KubernetesScriptStatusResponseV1Alpha> GetResponse(ScriptTicket ticket, long lastLogSequence, IRunningScript? runningScript)
        {
            await Task.CompletedTask;

            var workspace = workspaceFactory.GetWorkspace(ticket);
            var scriptLog = runningScript?.ScriptLog ?? workspace.CreateLog();
            var logs = scriptLog.GetOutput(lastLogSequence, out var next);

            if (runningScript != null)
            {
                return new KubernetesScriptStatusResponseV1Alpha(ticket, runningScript.State, runningScript.ExitCode, logs, next);
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

                return new KubernetesScriptStatusResponseV1Alpha(ticket, scriptState.State, scriptState.ExitCode ?? ScriptExitCodes.UnknownResultExitCode, logs, next);
            }

            return new KubernetesScriptStatusResponseV1Alpha(ticket, ProcessState.Complete, ScriptExitCodes.UnknownScriptExitCode, logs, next);
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