using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Octopus.Tentacle.Contracts;
using k8sClient = k8s.Kubernetes;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesJobService
    {
        Task<V1Job?> TryGet(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        string BuildJobName(ScriptTicket scriptTicket);
        Task CreateJob(V1Job job, CancellationToken cancellationToken);
        void Delete(ScriptTicket scriptTicket);
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
                return await Client.ReadNamespacedJobStatusAsync(jobName, KubernetesNamespace.Value, cancellationToken: cancellationToken);
            }
            catch (HttpOperationException opException)
            {
                if (opException.Response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                //if there is some other error, just throw the exception
                throw;
            }
        }

        public string BuildJobName(ScriptTicket scriptTicket) => $"octopus-{scriptTicket.TaskId}".ToLowerInvariant();

        public async Task CreateJob(V1Job job, CancellationToken cancellationToken)
        {
            await Client.CreateNamespacedJobAsync(job, KubernetesNamespace.Value, cancellationToken: cancellationToken);
        }

        public void Delete(ScriptTicket scriptTicket)
        {
            try
            {
                Client.DeleteNamespacedJob(BuildJobName(scriptTicket), KubernetesNamespace.Value);
            }
            catch
            {
                //we are comfortable silently consuming this as the jobs have a TTL that will clean it up anyway
            }
        }
    }
}