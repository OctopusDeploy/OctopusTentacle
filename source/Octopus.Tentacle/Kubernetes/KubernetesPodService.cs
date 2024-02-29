using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Nito.AsyncEx;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Polly;
using Polly.Retry;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        Task<V1Pod?> TryGetPod(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task<V1PodList> ListAllPods(CancellationToken cancellationToken);
        Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken);
        Task Create(V1Pod pod, CancellationToken cancellationToken);
        Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken);

#pragma warning disable CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
        IAsyncEnumerable<string?> StreamPodLogs(string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken = default);
#pragma warning restore CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
    }

    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        readonly ISystemLog log;
        readonly AsyncRetryPolicy<Stream> logRetryPolicy;

        public KubernetesPodService(IKubernetesClientConfigProvider configProvider, ISystemLog log)
            : base(configProvider)
        {
            this.log = log;

            var jitterer = new Random();
            logRetryPolicy = Policy<Stream>
                .Handle<HttpOperationException>()
                .WaitAndRetryForeverAsync((retryAttempt, _) =>
                        TimeSpan.FromMilliseconds(25 * Math.Pow(2, retryAttempt))
                        + TimeSpan.FromMilliseconds(jitterer.Next(0, 50)),
                    (result, _, _, ctx) =>
                    {
                        if (result.Exception is null)
                            return;

                        var podName = ctx["podName"];
                        if (result.Exception is HttpOperationException httpOp && httpOp.Response.StatusCode != HttpStatusCode.BadRequest)
                        {
                            log.Warn(result.Exception, $"Failed to read namespaced logs for pod {podName}. Response: {httpOp.Response.Content}");
                        }
                        else if (result.Exception is not HttpOperationException)
                        {
                            log.Warn(result.Exception, $"Failed to read namespaced logs for pod {podName}.");
                        }
                    });
        }

        public async Task<V1Pod?> TryGetPod(ScriptTicket scriptTicket, CancellationToken cancellationToken) =>
            await TryGetAsync(() => Client.ReadNamespacedPodAsync(scriptTicket.ToKubernetesScriptPobName(), KubernetesConfig.Namespace, cancellationToken: cancellationToken));

        public async Task<V1PodList> ListAllPods(CancellationToken cancellationToken)
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
                //resourceVersionMatch: "NotOlderThan",
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

            await foreach (var (type, pod) in response.WatchAsync<V1Pod, V1PodList>(internalOnError, cancellationToken: watchErrorCancellationTokenSource.Token))
            {
                await onChange(type, pod);
            }
        }

        public async IAsyncEnumerable<string?> StreamPodLogs(string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var enumerable = StreamPodLogsViaPolling(podName, containerName, cancellationToken);

            await foreach (var line in enumerable)
            {
                yield return line;
            }
        }

        async IAsyncEnumerable<string?> StreamPodLogsViaPolling(string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            DateTime? lastRetrievedTime = null;
            var hasReadEndOfScriptControlMessage = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var secondsSinceLastCheck = lastRetrievedTime.HasValue ? Math.Max((int)Math.Floor((now - lastRetrievedTime.Value).TotalSeconds), 1) : (int?)null;

                var retryContext = new Context
                {
                    ["podName"] = podName
                };
                //we use a polly retry policy to handle all the
                var logStream = await logRetryPolicy.ExecuteAsync(
                    async (_, ct) => await Client.ReadNamespacedPodLogAsync(podName,
                        KubernetesConfig.Namespace,
                        containerName,
                        sinceSeconds: secondsSinceLastCheck,
                        cancellationToken: ct),
                    retryContext,
                    cancellationToken);

                using var streamReader = new StreamReader(logStream);
                while (!streamReader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await streamReader.ReadLineAsync().ConfigureAwait(false);
                    if (line is not null)
                    {
                        log.Verbose($"Read log line {line} for pod {podName}");
                    }

                    yield return line;

                    if (line is not null && line.Contains(KubernetesConfig.EndOfScriptControlMessage))
                    {
                        hasReadEndOfScriptControlMessage = true;
                        break;
                    }
                }

                //don't loop again
                if (hasReadEndOfScriptControlMessage)
                    break;

                //we add the number of seconds onto the last retrieved time, just in case it took us a while to read the previous logs from the stream
                lastRetrievedTime = (lastRetrievedTime ?? now).AddSeconds(secondsSinceLastCheck.GetValueOrDefault(0));

                //delay for 1 second
                await Task.Delay(1000, cancellationToken);
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