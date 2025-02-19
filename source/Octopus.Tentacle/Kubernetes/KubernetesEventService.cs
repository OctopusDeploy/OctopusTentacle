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
        Task<Corev1EventList?> FetchAllEventsAsync(string podName, CancellationToken cancellationToken);
    }
    
    public class KubernetesEventService : KubernetesService, IKubernetesEventService
    {
        public KubernetesEventService(IKubernetesClientConfigProvider configProvider, IKubernetesConfiguration kubernetesConfiguration, ISystemLog log) 
            : base(configProvider, kubernetesConfiguration, log)
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

        public async Task<Corev1EventList?> FetchAllEventsAsync(string podName, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            { 
                try
                {
                    //get all the events for a specific script pod
                    return await Client.CoreV1.ListNamespacedEventAsync(
                        Namespace,
                        fieldSelector: $"involvedObject.name={podName}",
                        cancellationToken: cancellationToken);
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