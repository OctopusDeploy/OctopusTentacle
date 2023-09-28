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
        bool Exists(ScriptTicket scriptTicket);
        string BuildJobName(ScriptTicket scriptTicket);
        void CreateJob(V1Job job);
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

                //if there is some _other_ error, just throw the exception
                throw;
            }
        }

        public bool Exists(ScriptTicket scriptTicket)
        {
            return TryGet(scriptTicket) != null;
        }

        public string BuildJobName(ScriptTicket scriptTicket) => $"octopus-{scriptTicket.TaskId}".ToLowerInvariant();

        public void CreateJob(V1Job job)
        {
            Client.CreateNamespacedJob(job, KubernetesNamespace.Value);
        }

        static class Labels
        {
            public const string ScriptTicket = "octopus.com/script-ticket-id";
        }
    }
}