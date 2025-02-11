﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Kubernetes.Crypto;
using Octopus.Tentacle.Kubernetes.Synchronisation;
using Octopus.Tentacle.Maintenance;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Services.Scripts.Kubernetes
{
    [KubernetesService(typeof(IKubernetesScriptServiceV1))]
    public class KubernetesScriptServiceV1 : IAsyncKubernetesScriptServiceV1, IRunningScriptReporter
    {
        readonly IKubernetesPodService podService;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IKubernetesPodStatusProvider podStatusProvider;
        readonly IKubernetesScriptPodCreator scriptPodCreator;
        readonly IKubernetesRawScriptPodCreator rawScriptPodCreator;
        readonly IKubernetesPodLogService podLogService;
        readonly ITentacleScriptLogProvider scriptLogProvider;
        readonly IScriptPodSinceTimeStore scriptPodSinceTimeStore;
        readonly IScriptPodLogEncryptionKeyProvider scriptPodLogEncryptionKeyProvider;
        readonly IKeyedSemaphore<ScriptTicket> keyedSemaphore;

        public KubernetesScriptServiceV1(
            IKubernetesPodService podService,
            IScriptWorkspaceFactory workspaceFactory,
            IKubernetesPodStatusProvider podStatusProvider,
            IKubernetesScriptPodCreator scriptPodCreator,
            IKubernetesRawScriptPodCreator rawScriptPodCreator,
            IKubernetesPodLogService podLogService,
            ITentacleScriptLogProvider scriptLogProvider,
            IScriptPodSinceTimeStore scriptPodSinceTimeStore,
            IScriptPodLogEncryptionKeyProvider scriptPodLogEncryptionKeyProvider,
            IKeyedSemaphore<ScriptTicket> keyedSemaphore)
        {
            this.podService = podService;
            this.workspaceFactory = workspaceFactory;
            this.podStatusProvider = podStatusProvider;
            this.scriptPodCreator = scriptPodCreator;
            this.rawScriptPodCreator = rawScriptPodCreator;
            this.podLogService = podLogService;
            this.scriptLogProvider = scriptLogProvider;
            this.scriptPodSinceTimeStore = scriptPodSinceTimeStore;
            this.scriptPodLogEncryptionKeyProvider = scriptPodLogEncryptionKeyProvider;
            this.keyedSemaphore = keyedSemaphore;
        }

        public async Task<KubernetesScriptStatusResponseV1> StartScriptAsync(StartKubernetesScriptCommandV1 command, CancellationToken cancellationToken)
        {
            using (await keyedSemaphore.WaitAsync(command.ScriptTicket, cancellationToken))
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

                var logs = await CreatePodAndWaitForLogs(command, workspace, cancellationToken);

                return new KubernetesScriptStatusResponseV1(command.ScriptTicket, ProcessState.Pending, 0, logs.ToList(), 0);
            }
        }

        public async Task<KubernetesScriptStatusResponseV1> GetStatusAsync(KubernetesScriptStatusRequestV1 request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var trackedPod = podStatusProvider.TryGetTrackedScriptPod(request.ScriptTicket);
            return trackedPod != null
                ? await GetResponse(trackedPod, request.LastLogSequence, cancellationToken)
                : GetResponseForMissingScriptPod(request.ScriptTicket, request.LastLogSequence);
        }

        public async Task<KubernetesScriptStatusResponseV1> CancelScriptAsync(CancelKubernetesScriptCommandV1 command, CancellationToken cancellationToken)
        {
            var trackedPod = podStatusProvider.TryGetTrackedScriptPod(command.ScriptTicket);
            if (trackedPod == null)
                return GetResponseForMissingScriptPod(command.ScriptTicket, command.LastLogSequence);
            
            //Mark the tracked pod as completed
            trackedPod.MarkAsCompleted(ScriptExitCodes.CanceledExitCode, DateTimeOffset.UtcNow);
            
            //we write a cancellation message here (we put the pod message as verbose)
            var scriptLog = scriptLogProvider.GetOrCreate(command.ScriptTicket);
            scriptLog.Verbose($"Kubernetes Pod {command.ScriptTicket.ToKubernetesScriptPodName()} has been terminated.");
            scriptLog.Info("Script execution canceled.");
            
            //get the response for the tracked pod (which will now be marked as completed)
            var response = await GetResponse(trackedPod, command.LastLogSequence, cancellationToken);
            
            //delete the pod with a 0-second grace period
            await podService.DeleteIfExists(command.ScriptTicket, TimeSpan.Zero, cancellationToken);
            
            return response;
        }

        public async Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1 command, CancellationToken cancellationToken)
        {
            var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
            await workspace.Delete(cancellationToken);

            scriptLogProvider.Delete(command.ScriptTicket);
            scriptPodSinceTimeStore.Delete(command.ScriptTicket);
            scriptPodLogEncryptionKeyProvider.Delete(command.ScriptTicket);

            if (!KubernetesConfig.DisableAutomaticPodCleanup)
                await podService.DeleteIfExists(command.ScriptTicket, cancellationToken);
        }

        async Task<KubernetesScriptStatusResponseV1> GetResponse(ITrackedScriptPod trackedPod, long lastLogSequence, CancellationToken cancellationToken)
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

            var podStatusOutputs = new[]
            {
                //Help users notice if Pods are in the Pending state for too long
                new ProcessOutput(ProcessOutputSource.Debug, $"The Kubernetes Pod '{trackedPod.ScriptTicket.ToKubernetesScriptPodName()}' is in the '{state.Phase}' phase")
            };
            //Print the status first, since that's what we are basing our decisions on (printing it at the end might be confusing)
            var outputLogs = podStatusOutputs.Concat(podLogs).ToList();

            return new KubernetesScriptStatusResponseV1(
                trackedPod.ScriptTicket,
                processState,
                state.ExitCode ?? 0,
                outputLogs,
                nextLogSequence
            );
        }
        
        async Task<IReadOnlyCollection<ProcessOutput>> CreatePodAndWaitForLogs(StartKubernetesScriptCommandV1 command, IScriptWorkspace workspace, CancellationToken cancellationToken)
        {
            if (command.IsRawScript)
            {
                await rawScriptPodCreator.CreatePod(command, workspace, cancellationToken);
            }
            else
            {
                await scriptPodCreator.CreatePod(command, workspace, cancellationToken);
            }

            var (logs, _) = await podLogService.GetLogs(command.ScriptTicket, 0, cancellationToken);
            return logs;
        }

        static KubernetesScriptStatusResponseV1 GetResponseForMissingScriptPod(ScriptTicket scriptTicket, long lastLogSequence)
        {
            return new KubernetesScriptStatusResponseV1(scriptTicket,
                ProcessState.Complete,
                ScriptExitCodes.KubernetesScriptPodNotFound,
                new List<ProcessOutput>()
                {
                    new(ProcessOutputSource.StdErr, $"The Script Pod '{scriptTicket.ToKubernetesScriptPodName()}' could not be found. This is most likely due to the Script Pod being deleted."),
                    new(ProcessOutputSource.StdErr, $"Possible causes are:"),
                    new(ProcessOutputSource.StdErr, $"- The Script Pod was evicted/terminated by Kubernetes"),
                    new(ProcessOutputSource.StdErr, $"If you are using the default NFS storage, then also check if:"),
                    new(ProcessOutputSource.StdErr, $"- The NFS Pod was evicted due to exceeding its storage quota"),
                    new(ProcessOutputSource.StdErr, $"- The NFS Pod was restarted or moved as part of routine operation"),
                },
                lastLogSequence);
        }

        public bool IsRunningScript(ScriptTicket ticket)
        {
            return podStatusProvider.TryGetTrackedScriptPod(ticket) is not null;
        }
    }
}