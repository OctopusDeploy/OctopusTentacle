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
    public class KubernetesEventService : KubernetesService
    {
        public KubernetesEventService(IKubernetesClientConfigProvider configProvider, ISystemLog log) : base(configProvider, log)
        {
        }

        public async Task<Corev1EventList?> FetchAllEventsAsync(CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            { 
                try
                {
                    return await Client.CoreV1.ListEventForAllNamespacesAsync(cancellationToken: cancellationToken);
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