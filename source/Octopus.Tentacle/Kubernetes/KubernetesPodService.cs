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
        Task<V1PodList> ListAllPods(CancellationToken cancellationToken);
        Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, CancellationToken, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken);
        Task Create(V1Pod pod, CancellationToken cancellationToken);
        Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken);

#pragma warning disable CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
        IAsyncEnumerable<string?> StreamPodLogs(string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken = default);
#pragma warning restore CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
        Task TryDelete(ScriptTicket scriptTicket, CancellationToken cancellationToken);
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
            
                        // when the container is just starting you'll get errors like:
                        // k8s.Autorest.HttpOperationException: Operation returned an invalid status code 'BadRequest', response body {"kind":"Status","apiVersion":"v1","metadata":{},"status":"Failure","message":"container \"octopus-script-k2xfzpzymeuuhqwfw6vs3a\" in pod \"octopus-script-k2xfzpzymeuuhqwfw6vs3a\" is waiting to start: ContainerCreating","reason":"BadRequest","code":400}
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

        public async Task<V1PodList> ListAllPods(CancellationToken cancellationToken)
        {
            return await Client.ListNamespacedPodAsync(KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                cancellationToken: cancellationToken);
        }

        public async Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, CancellationToken, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken)
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

            try
            {
            	log.Verbose("Starting pod watch");
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
            ulong? lastReadLineHash =null;
            DateTimeOffset? lastReadTime = null;
            var hasReadEndOfScriptControlMessage = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                //read back one extra second just in case
                var sinceTime = lastReadTime?.Subtract(TimeSpan.FromSeconds(1));

                //update that we've read from now
                lastReadTime = DateTimeOffset.UtcNow;

                var retryContext = new Context
                {
                    ["podName"] = podName
                };
                //we use a polly retry policy to handle all the
                var logStream = await logRetryPolicy.ExecuteAsync(
                    async (_, ct) => await Client.GetNamespacedPodLogsAsync(podName,
                        KubernetesConfig.Namespace,
                        containerName,
                        sinceTime: sinceTime,
                        cancellationToken: ct),
                    retryContext,
                    cancellationToken);
 
                using var streamReader = new StreamReader(logStream);
                string? lastLine = null;
                var hasSeenLastReadLine = false;
                while (!streamReader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await streamReader.ReadLineAsync().ConfigureAwait(false);

                    // if there was a last line read and we haven't passed it
                    if (lastReadLineHash.HasValue && !hasSeenLastReadLine && line is not null)
                    {
                        //calculate the new line hash
                        var hash = CalculateHash(line);

                        //if the hashes are the same, the we have reached the last line read
                        hasSeenLastReadLine = lastReadLineHash == hash;

                        var description = hasSeenLastReadLine ? "Is" : "Is not";
                        //log.Verbose($"Read log line {line} for pod {podName} but we have seen it before so ignoring. {description} the last read line.");

                        //continue as we'll now be starting from new logs (if there are any
                        continue;
                    }

                    // if (line is not null)
                    // {
                    //     log.Verbose($"Read new log line {line} for pod {podName}");
                    // }

                    yield return line;

                    lastLine = line;
                    if (line is not null && line.Contains(KubernetesConfig.EndOfScriptControlMessage))
                    {
                        hasReadEndOfScriptControlMessage = true;
                        break;
                    }
                }

                //don't loop again
                if (hasReadEndOfScriptControlMessage)
                    break;

                if (lastLine is not null)
                {
                    lastReadLineHash = CalculateHash(lastLine);
                }

                //delay for 1 second
                await Task.Delay(1000, cancellationToken);
            }
        }

        static ulong CalculateHash(string read)
        {
            var hashedValue = 3074457345618258791ul;
            for (var i = 0; i < read.Length; i++)
            {
                hashedValue += read[i];
                hashedValue *= 3074457345618258799ul;
            }

            return hashedValue;
        }

        public async Task Create(V1Pod pod, CancellationToken cancellationToken)
        {
            AddStandardMetadata(pod);
            await Client.CreateNamespacedPodAsync(pod, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }

        public async Task Delete(ScriptTicket scriptTicket, CancellationToken cancellationToken)
            => await Client.DeleteNamespacedPodAsync(scriptTicket.ToKubernetesScriptPobName(), KubernetesConfig.Namespace, cancellationToken: cancellationToken);

        public async Task TryDelete(ScriptTicket scriptTicket, CancellationToken cancellationToken)
            => await TryExecuteAsync(async () => await Delete(scriptTicket, cancellationToken));
    }
}