using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesEventService
    {
        Task<Corev1EventList?> FetchAllEventsAsync(CancellationToken cancellationToken);
    }
    
    public class KubernetesEventService : KubernetesService, IKubernetesEventService
    {
        public KubernetesEventService(IKubernetesClientConfigProvider configProvider,IKubernetesConfiguration kubernetesConfiguration,  ISystemLog log) 
            : base(configProvider, kubernetesConfiguration,log)
        {
        }

        public async Task<Corev1EventList?> FetchAllEventsAsync(CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            { 
                try
                {
                    return await Client.CoreV1.ListNamespacedEventAsync(Namespace, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException opException)
                    when (opException.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
            });
        }
    }
}