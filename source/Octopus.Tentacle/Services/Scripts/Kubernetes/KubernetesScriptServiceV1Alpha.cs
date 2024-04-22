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

                //Note:
                //We shouldn't really need to worry about starting the same script twice,
                //since we wait for the Pods to be reloaded from the K8s API on Tentacle restart.
                //We might try to start the same script twice if
                // - a Pod gets created
                // - Tentacle restarts before returning
                // - Server retries StartScriptAsync()
                // - The Kubernetes API doesn't say that the Pod exists when we query it.
                
                //Note: PrepareWorkspace overwrites "command.Files", so it's not strictly idempotent
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
                : throw new MissingScriptPodException(request.ScriptTicket);
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptAsync(CancelKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken)
        {
            var trackedPod = podStatusProvider.TryGetTrackedScriptPod(command.ScriptTicket);
            //if we are cancelling a pod that doesn't exist, just return complete with an unknown script exit code
            if (trackedPod == null)
                return new KubernetesScriptStatusResponseV1Alpha(command.ScriptTicket, ProcessState.Complete, ScriptExitCodes.UnknownScriptExitCode, new List<ProcessOutput>(), command.LastLogSequence);

            var response = await GetResponse(trackedPod, command.LastLogSequence, cancellationToken);

            await podService.DeleteIfExists(command.ScriptTicket, cancellationToken);

            return response;
        }

        public async Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken)
        {
            startScriptMutexes.TryRemove(command.ScriptTicket, out _);

            var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
            await workspace.Delete(cancellationToken);

            scriptLogProvider.Delete(command.ScriptTicket);
            scriptPodSinceTimeStore.Delete(command.ScriptTicket);
            
            if (!KubernetesConfig.DisableAutomaticPodCleanup)
                await podService.DeleteIfExists(command.ScriptTicket, cancellationToken);
        }

        async Task<KubernetesScriptStatusResponseV1Alpha> GetResponse(ITrackedScriptPod trackedPod, long lastLogSequence, CancellationToken cancellationToken)
        {
            var state = trackedPod.State;
            var processState = state.Phase switch
            {
                TrackedScriptPodPhase.Pending => ProcessState.Pending,
                TrackedScriptPodPhase.Running => ProcessState.Running,
                TrackedScriptPodPhase.Succeeded => ProcessState.Complete,
                TrackedScriptPodPhase.Failed => ProcessState.Complete,
                _ => throw new ArgumentOutOfRangeException()
            };

            var (podLogs, nextLogSequence) = await podLogService.GetLogs(trackedPod.ScriptTicket, lastLogSequence, cancellationToken);

            var podStatusOutputs = new []
            {
                //Help users notice if Pods are in the Pending state for too long
                new ProcessOutput(ProcessOutputSource.Debug, $"The Kubernetes Pod '{trackedPod.ScriptTicket.ToKubernetesScriptPodName()}' is in the '{state.Phase}' phase")
            };
            //Print the status first, since that's what we are basing our decisions on (printing it at the end might be confusing)
            var outputLogs = podStatusOutputs.Concat(podLogs).ToList();

            return new KubernetesScriptStatusResponseV1Alpha(
                trackedPod.ScriptTicket,
                processState,
                state.ExitCode ?? 0,
                outputLogs,
                nextLogSequence
            );
        }

        public bool IsRunningScript(ScriptTicket ticket)
        {
            return podStatusProvider.TryGetTrackedScriptPod(ticket) is not null;
        }
    }
    
    class MissingScriptPodException : Exception
    {
        public MissingScriptPodException(ScriptTicket scriptTicket)
            : base($"The Script Pod for script ticket '{scriptTicket}' could not be found, please retry the action")
        {
        }
    }

}