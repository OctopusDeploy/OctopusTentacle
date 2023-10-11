using System;
using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Octopus.Tentacle.Contracts;
using k8sClient = k8s.Kubernetes;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesJobService
    {
        V1Job? TryGet(ScriptTicket scriptTicket);
        string BuildJobName(ScriptTicket scriptTicket);
        void CreateJob(V1Job job);
        void Delete(ScriptTicket scriptTicket);
    }

    public class KubernetesJobService : KubernetesService, IKubernetesJobService
    {
        public KubernetesJobService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
        }

        public V1Job? TryGet(ScriptTicket scriptTicket)
        {
            var jobName = BuildJobName(scriptTicket);

            try
            {
                return Client.ReadNamespacedJobStatus(jobName, KubernetesNamespace.Value);
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

        public void CreateJob(V1Job job)
        {
            Client.CreateNamespacedJob(job, KubernetesNamespace.Value);
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