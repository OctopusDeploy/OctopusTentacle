using System;
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

        public string BuildJobName(ScriptTicket scriptTicket) => $"octopus-job-{scriptTicket.TaskId}".ToLowerInvariant();

        public async Task CreateJob(V1Job job, CancellationToken cancellationToken)
        {
            AddStandardMetadata(job.Metadata);
            await Client.CreateNamespacedJobAsync(job, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }

        public async Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken) => await Client.DeleteNamespacedJobAsync(BuildJobName(scriptTicket), KubernetesConfig.Namespace, cancellationToken: cancellationToken);
    }
}