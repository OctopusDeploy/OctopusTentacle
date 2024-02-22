﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesJobService
    {
        string BuildJobName(ScriptTicket scriptTicket);
        Task CreateJob(V1Job job, CancellationToken cancellationToken);
        Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task Watch(ScriptTicket scriptTicket, Func<V1Job, bool> onChange, Action<Exception> onError, CancellationToken cancellationToken);
        Task SuspendJob(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task WatchAllJobsAsync(string initialResourceVersion, Func<WatchEventType, V1Job, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken);
        Task<V1JobList> ListAllJobsAsync(CancellationToken cancellationToken);
    }

    public class KubernetesJobService : KubernetesService, IKubernetesJobService
    {
        public KubernetesJobService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
        }

        public async Task SuspendJob(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            var jobName = BuildJobName(scriptTicket);

            var patchJob = new V1Job
            {
                Spec = new V1JobSpec
                {
                    Suspend = true
                }
            };
            var patchYaml = KubernetesJson.Serialize(patchJob);
            await Client.PatchNamespacedJobAsync(new V1Patch(patchYaml, V1Patch.PatchType.MergePatch), jobName, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }

        public async Task Watch(ScriptTicket scriptTicket, Func<V1Job, bool> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            var jobName = BuildJobName(scriptTicket);

            using var response = Client.BatchV1.ListNamespacedJobWithHttpMessagesAsync(
                KubernetesConfig.Namespace,
                //only list this job
                fieldSelector: $"metadata.name=={jobName}",
                watch: true,
                timeoutSeconds: KubernetesConfig.JobMonitorTimeoutSeconds,
                cancellationToken: cancellationToken);

            await foreach (var (type, job) in response.WatchAsync<V1Job, V1JobList>(onError, cancellationToken: cancellationToken))
            {
                //watch for modifications and deletions
                if (type is not (WatchEventType.Modified or WatchEventType.Deleted))
                    continue;

                var stopWatching = onChange(job);
                //we stop watching when told to or if this is deleted
                if (stopWatching || type is WatchEventType.Deleted)
                    break;
            }
        }

        public async Task<V1JobList> ListAllJobsAsync(CancellationToken cancellationToken)
        {
            return await Client.ListNamespacedJobAsync(KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                cancellationToken: cancellationToken);
        }

        public async Task WatchAllJobsAsync(string initialResourceVersion, Func<WatchEventType, V1Job, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            using var response = Client.BatchV1.ListNamespacedJobWithHttpMessagesAsync(
                KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                resourceVersion: initialResourceVersion,
                watch: true,
                cancellationToken: cancellationToken);

            var watchErrorCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Action<Exception> internalOnError = ex =>
            {
                //We cancel the watch explicitly (so it can be restarted)
                watchErrorCancellationTokenSource.Cancel();

                //notify there was an error
                onError(ex);
            };

            await foreach (var (type, job) in response.WatchAsync<V1Job, V1JobList>(internalOnError, cancellationToken: watchErrorCancellationTokenSource.Token))
            {
                await onChange(type, job);
            }
        }

        public string BuildJobName(ScriptTicket scriptTicket) => $"octopus-job-{scriptTicket.TaskId}".ToLowerInvariant();

        public async Task CreateJob(V1Job job, CancellationToken cancellationToken)
        {
            AddStandardMetadata(job);
            await Client.CreateNamespacedJobAsync(job, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }

        public async Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken) => await Client.DeleteNamespacedJobAsync(BuildJobName(scriptTicket), KubernetesConfig.Namespace, cancellationToken: cancellationToken);
    }
}