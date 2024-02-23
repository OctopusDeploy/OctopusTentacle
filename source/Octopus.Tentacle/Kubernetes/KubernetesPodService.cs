using System;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        Task Create(V1Pod pod, CancellationToken cancellationToken);
        Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task Watch(ScriptTicket scriptTicket, Func<V1Pod, bool> onChange, Action<Exception> onError, CancellationToken cancellationToken);
    }

    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        public KubernetesPodService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
        }

        public async Task Watch(ScriptTicket scriptTicket, Func<V1Pod, bool> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            using var response = Client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
                KubernetesConfig.Namespace,
                //only list this pod
                fieldSelector: $"metadata.name=={scriptTicket.ToKubernetesScriptPobName()}",
                watch: true,
                timeoutSeconds: KubernetesConfig.PodMonitorTimeoutSeconds,
                cancellationToken: cancellationToken);

            await foreach (var (type, pod) in response.WatchAsync<V1Pod, V1PodList>(onError, cancellationToken: cancellationToken))
            {
                //watch for modifications and deletions
                if (type is not (WatchEventType.Modified or WatchEventType.Deleted))
                    continue;

                var stopWatching = onChange(pod);
                //we stop watching when told to or if this is deleted
                if (stopWatching || type is WatchEventType.Deleted)
                    break;
            }
        }

        public async Task Create(V1Pod pod, CancellationToken cancellationToken)
        {
            AddStandardMetadata(pod);
            await Client.CreateNamespacedPodAsync(pod, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }

        public async Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken)
            => await Client.DeleteNamespacedPodAsync(scriptTicket.ToKubernetesScriptPobName(), KubernetesConfig.Namespace, cancellationToken: cancellationToken);
    }
}