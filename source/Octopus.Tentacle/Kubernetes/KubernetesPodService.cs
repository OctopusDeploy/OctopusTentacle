using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        Task<V1Pod?> TryGetPod(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task<V1PodList> ListAllPodsAsync(CancellationToken cancellationToken);
        Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken);
        Task Create(V1Pod pod, CancellationToken cancellationToken);
        Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task<string> GetLogs(ScriptTicket scriptTicket, CancellationToken cancellationToken);
    }

    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        public KubernetesPodService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
        }

        public async Task<string> GetLogs(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            
            using var response = await Client.ReadNamespacedPodLogAsync(
                scriptTicket.ToKubernetesScriptPobName(),
                KubernetesConfig.Namespace,
                cancellationToken: cancellationToken);

            using var reader = new StreamReader(response);

            return await reader.ReadToEndAsync();
        }

        public async Task<V1Pod?> TryGetPod(ScriptTicket scriptTicket, CancellationToken cancellationToken) =>
            await TryGetAsync(() => Client.ReadNamespacedPodAsync(scriptTicket.ToKubernetesScriptPobName(), KubernetesConfig.Namespace, cancellationToken: cancellationToken));

        public async Task<V1PodList> ListAllPodsAsync(CancellationToken cancellationToken)
        {
            return await Client.ListNamespacedPodAsync(KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                cancellationToken: cancellationToken);
        }

        public async Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            using var response = Client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
                KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                resourceVersion: initialResourceVersion,
                watch: true,
                timeoutSeconds: KubernetesConfig.PodMonitorTimeoutSeconds,
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
                    await onChange(type, pod);
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
        
        public async Task Create(V1Pod pod, CancellationToken cancellationToken)
        {
            AddStandardMetadata(pod);
            await Client.CreateNamespacedPodAsync(pod, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }

        public async Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken)
            => await Client.DeleteNamespacedPodAsync(scriptTicket.ToKubernetesScriptPobName(), KubernetesConfig.Namespace, cancellationToken: cancellationToken);
    }
}