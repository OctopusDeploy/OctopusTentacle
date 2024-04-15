using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Maintenance;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.Scripts.Kubernetes
{
    [KubernetesService(typeof(IKubernetesScriptServiceV1Alpha))]
    public class KubernetesScriptServiceV1Alpha : IAsyncKubernetesScriptServiceV1Alpha, IRunningScriptReporter
    {
        readonly IKubernetesPodService podService;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IKubernetesPodStatusProvider podStatusProvider;
        readonly IKubernetesScriptPodCreator podCreator;
        readonly IKubernetesPodLogService podLogService;
        readonly ISystemLog log;
        readonly ITentacleScriptLogProvider scriptLogProvider;
        readonly IScriptPodSinceTimeStore scriptPodSinceTimeStore;
        
        //TODO: check what will happen when Tentacle restarts
        readonly ConcurrentDictionary<ScriptTicket, Lazy<SemaphoreSlim>> startScriptMutexes = new();

        public KubernetesScriptServiceV1Alpha(
            IKubernetesPodService podService,
            IScriptWorkspaceFactory workspaceFactory,
            IKubernetesPodStatusProvider podStatusProvider,
            IKubernetesScriptPodCreator podCreator,
            IKubernetesPodLogService podLogService,
            ISystemLog log, ITentacleScriptLogProvider scriptLogProvider, IScriptPodSinceTimeStore scriptPodSinceTimeStore)
        {
            this.podService = podService;
            this.workspaceFactory = workspaceFactory;
            this.podStatusProvider = podStatusProvider;
            this.podCreator = podCreator;
            this.podLogService = podLogService;
            this.log = log;
            this.scriptLogProvider = scriptLogProvider;
            this.scriptPodSinceTimeStore = scriptPodSinceTimeStore;
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> StartScriptAsync(StartKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken)
        {
            var mutex = startScriptMutexes.GetOrAdd(command.ScriptTicket, _ => new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(1, 1))).Value;
            using (await mutex.LockAsync(cancellationToken))
            {
                var trackedPod = podStatusProvider.TryGetTrackedScriptPod(command.ScriptTicket);
                if (trackedPod != null)
                {
                    return await GetResponse(trackedPod, 0, cancellationToken);
                }

                //TODO: consider adding an idempotent version of PrepareWorkspace
                var workspace = await workspaceFactory.PrepareWorkspace(command.ScriptTicket,
                    command.ScriptBody,
                    command.Scripts,
                    command.Isolation,
                    command.ScriptIsolationMutexTimeout,
                    command.IsolationMutexName,
                    command.Arguments,
                    command.Files,
                    cancellationToken);

                //create the pod
                await podCreator.CreatePod(command, workspace, cancellationToken);

                var (logs, _) = await podLogService.GetLogs(command.ScriptTicket, 0, cancellationToken);

                //return a status that say's we are pending
                return new KubernetesScriptStatusResponseV1Alpha(command.ScriptTicket, ProcessState.Pending, 0, logs.ToList(), 0);
            }
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> GetStatusAsync(KubernetesScriptStatusRequestV1Alpha request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var trackedPod = podStatusProvider.TryGetTrackedScriptPod(request.ScriptTicket);
            return trackedPod != null
                ? await GetResponse(trackedPod, request.LastLogSequence, cancellationToken)
                : throw new Exception("Can't find Script Pod for: " + request.ScriptTicket);
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptAsync(CancelKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken)
        {
            var trackedPod = podStatusProvider.TryGetTrackedScriptPod(command.ScriptTicket);
            //if we are cancelling a pod that doesn't exist, just return complete with an unknown script exit code
            if (trackedPod == null)
                return new KubernetesScriptStatusResponseV1Alpha(command.ScriptTicket, ProcessState.Complete, ScriptExitCodes.UnknownScriptExitCode, new List<ProcessOutput>(), command.LastLogSequence);

            var response = await GetResponse(trackedPod, command.LastLogSequence, cancellationToken);

            //delete the pod
            await podService.Delete(command.ScriptTicket, cancellationToken);

            return response;
        }

        public async Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken)
        {
            startScriptMutexes.TryRemove(command.ScriptTicket, out _);

            var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
            await workspace.Delete(cancellationToken);

            scriptLogProvider.Delete(command.ScriptTicket);
            scriptPodSinceTimeStore.Delete(command.ScriptTicket);
            
            //we do a try delete as the cancel might have already deleted it
            if (!KubernetesConfig.DisableAutomaticPodCleanup)
                await podService.TryDelete(command.ScriptTicket, cancellationToken);
        }

        async Task<KubernetesScriptStatusResponseV1Alpha> GetResponse(ITrackedScriptPod trackedPod, long lastLogSequence, CancellationToken cancellationToken)
        {
            var processState = trackedPod.State switch
            {
                TrackedScriptPodState.Running => ProcessState.Running,
                TrackedScriptPodState.Succeeded => ProcessState.Complete,
                TrackedScriptPodState.Failed => ProcessState.Complete,
                _ => throw new ArgumentOutOfRangeException()
            };

            var (outputLogs, nextLogSequence) = await podLogService.GetLogs(trackedPod.ScriptTicket, lastLogSequence, cancellationToken);

            return new KubernetesScriptStatusResponseV1Alpha(
                trackedPod.ScriptTicket,
                processState,
                trackedPod.ExitCode ?? 0,
                outputLogs.ToList(),
                nextLogSequence
            );
        }

        public bool IsRunningScript(ScriptTicket ticket)
        {
            return podStatusProvider.TryGetTrackedScriptPod(ticket) is not null;
        }
    }
}