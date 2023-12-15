using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesJobService
    {
        Task<V1Job?> TryGet(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        string BuildJobName(ScriptTicket scriptTicket);
        Task CreateJob(V1Job job, CancellationToken cancellationToken);
        Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task Watch(ScriptTicket scriptTicket, Func<V1Job, bool> onChange, Action<Exception> onError, CancellationToken cancellationToken);
    }

    public class KubernetesJobService : KubernetesService, IKubernetesJobService
    {
        public KubernetesJobService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
        }

        public async Task<V1Job?> TryGet(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            var jobName = BuildJobName(scriptTicket);

            try
            {
                return await Client.ReadNamespacedJobStatusAsync(jobName, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
            }
            catch (HttpOperationException opException)
                when (opException.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task Watch(ScriptTicket scriptTicket, Func<V1Job, bool> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            var jobName = BuildJobName(scriptTicket);

            using var response = Client.BatchV1.ListNamespacedJobWithHttpMessagesAsync(
                KubernetesConfig.Namespace,
                //only list this job
                fieldSelector: $"metadata.name=={jobName}",
                watch: true,
                timeoutSeconds: KubernetesConfig.JobTtlSeconds,
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

        public string BuildJobName(ScriptTicket scriptTicket) => $"octopus-{scriptTicket.TaskId}".ToLowerInvariant();

        public async Task CreateJob(V1Job job, CancellationToken cancellationToken) => await Client.CreateNamespacedJobAsync(job, KubernetesConfig.Namespace, cancellationToken: cancellationToken);

        public async Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken) => await Client.DeleteNamespacedJobAsync(BuildJobName(scriptTicket), KubernetesConfig.Namespace, cancellationToken: cancellationToken);
    }
}