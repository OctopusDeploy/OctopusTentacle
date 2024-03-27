using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesLogService
    {
        Task<Stream> GetLogs(string podName, string namespaceParameter, string container, DateTimeOffset? sinceTime = null, CancellationToken cancellationToken = default);
    }

    public class KubernetesLogService : KubernetesService, IKubernetesLogService
    {
        public KubernetesLogService(IKubernetesClientConfigProvider configProvider) : base(configProvider)
        {
        }

        public async Task<Stream> GetLogs(string podName, string namespaceParameter, string container, DateTimeOffset? sinceTime = null, CancellationToken cancellationToken = default)
        {
            return await Client.GetNamespacedPodLogsAsync(podName, namespaceParameter, container, sinceTime, cancellationToken);
        }
    }
}