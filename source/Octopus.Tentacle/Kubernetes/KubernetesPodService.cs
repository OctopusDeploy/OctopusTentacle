using System.Linq;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using k8sClient = k8s.Kubernetes;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        V1Pod? TryGet(V1Job job);
    }


    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        private readonly ILog log;

        public KubernetesPodService(IKubernetesClientConfigProvider configProvider, ILog log)
            : base(configProvider)
        {
            this.log = log;
        }

        public V1Pod? TryGet(V1Job job)
        {
            var pods = Client.ListNamespacedPod(KubernetesNamespace.Value,
                labelSelector: $"job-name=={job.Metadata.Name}");

            if(pods.Items.Count > 1)
                log.Warn($"There were multiple pods associated with job {job.Metadata.Name}. This is currently unsupported and may result in unexpected behaviour. Using the first pod.");

            return pods.Items.FirstOrDefault();
        }
    }
}