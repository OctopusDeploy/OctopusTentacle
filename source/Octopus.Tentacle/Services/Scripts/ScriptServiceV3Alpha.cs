using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;
using Polly;
using Polly.Timeout;

namespace Octopus.Tentacle.Services.Scripts
{
    [Service(typeof(IScriptServiceV3Alpha))]
    public class ScriptServiceV3Alpha : IAsyncScriptServiceV3Alpha
    {
        readonly IKubernetesPodService podService;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IKubernetesPodStatusProvider statusProvider;
        readonly IKubernetesScriptPodCreator podCreator;
        readonly ISystemLog log;

        readonly ConcurrentDictionary<ScriptTicket, Lazy<SemaphoreSlim>> startScriptMutexes = new();

        public ScriptServiceV3Alpha(
            IKubernetesPodService podService,
            IScriptWorkspaceFactory workspaceFactory,
            IKubernetesPodStatusProvider statusProvider,
            IKubernetesScriptPodCreator podCreator,
            ISystemLog log)
        {
            this.podService = podService;
            this.workspaceFactory = workspaceFactory;
            this.statusProvider = statusProvider;
            this.podCreator = podCreator;
            this.log = log;
        }

        public async Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, CancellationToken cancellationToken)
        {
            var mutex = startScriptMutexes.GetOrAdd(command.ScriptTicket, _ => new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(1, 1))).Value;

            using (await mutex.LockAsync(cancellationToken))
            {
                var trackedPod = statusProvider.TryGetPodStatus(command.ScriptTicket);
                if (trackedPod != null)
                {
                    return GetResponse(trackedPod, 0);
                }

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

                return new ScriptStatusResponseV3Alpha(command.ScriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0);
            }
        }

        public async Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var trackedPod = statusProvider.TryGetPodStatus(request.ScriptTicket);
            return trackedPod != null
                ? GetResponse(trackedPod, request.LastLogSequence)
                //if we are getting the status of an unknown pod, return that it's still pending
                : new ScriptStatusResponseV3Alpha(request.ScriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), request.LastLogSequence);
        }

        public async Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, CancellationToken cancellationToken)
        {
            var trackedPod = statusProvider.TryGetPodStatus(command.ScriptTicket);
            //if we are cancelling a pod that doesn't exist, just return complete with an unknown script exit code
            if (trackedPod == null)
                return new ScriptStatusResponseV3Alpha(command.ScriptTicket, ProcessState.Complete, ScriptExitCodes.UnknownScriptExitCode, new List<ProcessOutput>(), command.LastLogSequence);

            var response = GetResponse(trackedPod, command.LastLogSequence);

            //delete the pod
            await podService.Delete(command.ScriptTicket, cancellationToken);

            return response;
        }

        public async Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, CancellationToken cancellationToken)
        {
            startScriptMutexes.TryRemove(command.ScriptTicket, out _);

            var workspace = workspaceFactory.GetWorkspace(command.ScriptTicket);
            await workspace.Delete(cancellationToken);

            //we do a try delete as the cancel might have already deleted it
            if (!KubernetesConfig.DisableAutomaticPodCleanup)
                await podService.TryDelete(command.ScriptTicket, cancellationToken);
        }

        static ScriptStatusResponseV3Alpha GetResponse(ITrackedKubernetesPod trackedPod, long lastLogSequence)
        {
            var processState = trackedPod.State switch
            {
                TrackedPodState.Running => ProcessState.Running,
                TrackedPodState.Succeeded => ProcessState.Complete,
                TrackedPodState.Failed => ProcessState.Complete,
                _ => throw new ArgumentOutOfRangeException()
            };

            var (nextLogSequence, logLines) = trackedPod.GetLogs(lastLogSequence);

            var outputLogs = logLines.Select(ll => new ProcessOutput(ll.Source, ll.Message, ll.Occurred)).ToList();

            return new ScriptStatusResponseV3Alpha(trackedPod.ScriptTicket,
                processState,
                trackedPod.ExitCode ?? 0,
                outputLogs,
                nextLogSequence
            );
        }

        public bool IsRunningScript(ScriptTicket ticket)
        {
            return statusProvider.TryGetPodStatus(ticket) is not null;
        }
    }
}