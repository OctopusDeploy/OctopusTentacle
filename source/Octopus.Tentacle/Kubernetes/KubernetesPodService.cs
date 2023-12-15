using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        Task DeletePodsForJob(string jobName, CancellationToken cancellationToken);
    }

    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        public KubernetesPodService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        { }

        public async Task DeletePodsForJob(string jobName, CancellationToken cancellationToken)
        {
            var pods = await Client.ListNamespacedPodAsync(KubernetesConfig.Namespace, labelSelector: $"job-name=={jobName}", cancellationToken: cancellationToken);

            //for all the pods (should only be 1, but hey)
            foreach (var pod in pods)
            {
                //Delete the pod (forcing graceful termination)
                await Client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace(), cancellationToken: cancellationToken);
            }
        }
    }
}