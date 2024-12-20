using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        Task<V1PodList> ListAllPods(CancellationToken cancellationToken);
        Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, CancellationToken, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken);
        Task<V1Pod> Create(V1Pod pod, CancellationToken cancellationToken);
        Task DeleteIfExists(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task DeleteIfExists(ScriptTicket scriptTicket, TimeSpan gracePeriod, CancellationToken cancellationToken);
    }

    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        public KubernetesPodService(IKubernetesClientConfigProvider configProvider, IKubernetesConfiguration kubernetesConfiguration, ISystemLog log)
            : base(configProvider, kubernetesConfiguration,log)
        {
        }

        public async Task<V1PodList> ListAllPods(CancellationToken cancellationToken)
        {
            return await Client.ListNamespacedPodAsync(Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                cancellationToken: cancellationToken);
        }

        public async Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, CancellationToken, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            using var response = Client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
                Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                resourceVersion: initialResourceVersion,
                watch: true,
                timeoutSeconds: KubernetesConfiguration.ScriptPodMonitorTimeoutSeconds,
                cancellationToken: cancellationToken);

            var watchErrorCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Action<Exception> internalOnError = ex =>
            {
                //We cancel the watch explicitly (so it can be restarted)
                watchErrorCancellationTokenSource.Cancel();

                //notify there was an error
                onError(ex);
            };

            try
            {
                await foreach (var (type, pod) in response.WatchAsync<V1Pod, V1PodList>(internalOnError, cancellationToken: watchErrorCancellationTokenSource.Token))
                {
                    await onChange(type, pod, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                //Unfortunately we get an exception when the timeout hits (Server closes the connection)
                //https://github.com/kubernetes-client/csharp/issues/828
                if (ex is EndOfStreamException || ex.InnerException is EndOfStreamException)
                {
                    //Watch closed by api server, ignore this exception
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task<V1Pod> Create(V1Pod pod, CancellationToken cancellationToken)
        {
            AddStandardMetadata(pod);
            return await Client.CreateNamespacedPodAsync(pod, Namespace, cancellationToken: cancellationToken);
        }

        public async Task DeleteIfExists(ScriptTicket scriptTicket, CancellationToken cancellationToken)
            => await DeleteIfExistsInternal(scriptTicket, null, cancellationToken);

        public async Task DeleteIfExists(ScriptTicket scriptTicket, TimeSpan gracePeriod, CancellationToken cancellationToken) 
            => await DeleteIfExistsInternal(scriptTicket, (long)Math.Floor(gracePeriod.TotalSeconds), cancellationToken);

        async Task DeleteIfExistsInternal(ScriptTicket scriptTicket, long? gracePeriodSeconds, CancellationToken cancellationToken)
            => await TryExecuteAsync(async () => await Client.DeleteNamespacedPodAsync(scriptTicket.ToKubernetesScriptPodName(), Namespace, new V1DeleteOptions(gracePeriodSeconds: gracePeriodSeconds), cancellationToken: cancellationToken));
    }
}