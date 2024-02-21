using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        Task<V1Pod?> TryGetPodForJob(ScriptTicket scriptTicket, CancellationToken cancellationToken);
    }

    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        public KubernetesPodService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
        }

        public async Task<V1Pod?> TryGetPodForJob(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            var jobPods = await Client.ListNamespacedPodAsync(
                KubernetesConfig.Namespace,
                labelSelector:$"job-name={scriptTicket.ToKubernetesJobName()}",
                //we limit to 2 so we can error if there is more than 2 (see below)
                limit: 2,
                cancellationToken: cancellationToken);

            //there should only ever be one pod, so let's error if there isn't
            return jobPods.Items.SingleOrDefault();
        }
    }
}