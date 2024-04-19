using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s.Autorest;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Time;
using Polly;
using Polly.Retry;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodLogService
    {
        Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken);
    }

    class KubernetesPodLogService : KubernetesService, IKubernetesPodLogService
    {
        readonly IKubernetesPodMonitor podMonitor;
        readonly ITentacleScriptLogProvider scriptLogProvider;
        readonly IScriptPodSinceTimeStore scriptPodSinceTimeStore;
        readonly ISystemLog log;
        AsyncRetryPolicy retryPolicy;

        const int MaxDurationSeconds = 30;

        public KubernetesPodLogService(IKubernetesClientConfigProvider configProvider, IKubernetesPodMonitor podMonitor, ITentacleScriptLogProvider scriptLogProvider, IScriptPodSinceTimeStore scriptPodSinceTimeStore, ISystemLog log) 
            : base(configProvider)
        {
            this.podMonitor = podMonitor;
            this.scriptLogProvider = scriptLogProvider;
            this.scriptPodSinceTimeStore = scriptPodSinceTimeStore;
            this.log = log;
            
            retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(5,
                retry => TimeSpan.FromSeconds(ExponentialBackoff.GetDuration(retry, MaxDurationSeconds)),
                (ex, duration) =>
                {
                    log.Error(ex, "An unexpected error occured while querying Pod logs, waiting for: " + duration);
                });
        }

        public async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken)
        {
            var tentacleScriptLog = scriptLogProvider.GetOrCreate(scriptTicket);
            var podName = scriptTicket.ToKubernetesScriptPodName();
            var sinceTime = scriptPodSinceTimeStore.GetSinceTime(scriptTicket);

            (IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber, int? ExitCode) podLogs;

            try
            {
                using (var logStream = await GetLogStream(podName, sinceTime, cancellationToken))
                {
                    if (logStream == null)
                        return (new List<ProcessOutput>(), lastLogSequence);

                    podLogs = await ReadPodLogs(logStream);
                }
            }
            catch (UnexpectedPodLogLineNumberException ex)
            {
                using (var entireStream = await GetLogStream(podName, null, cancellationToken))
                {
                    var reader = new StreamReader(entireStream!);
                    var allLogs = await reader.ReadToEndAsync();

                    throw new Exception($"Pod log number weird (sinceTime: {sinceTime}), whole logs: {allLogs}", ex);
                }
            }

            if (podLogs.Outputs.Any())
            {
                var nextSinceTime = podLogs.Outputs.Max(o => o.Occurred);
                scriptPodSinceTimeStore.UpdateSinceTime(scriptTicket, nextSinceTime);
                
                //We can use our EOS marker to detect completion quicker than the Pod status
                if (podLogs.ExitCode != null)
                    podMonitor.MarkAsCompleted(scriptTicket, podLogs.ExitCode.Value);
            }

            //We are making the assumption that the clock on the Tentacle Pod is in sync with API Server
            var tentacleLogs = tentacleScriptLog.PopLogs();
            var combinedLogs = podLogs.Outputs.Concat(tentacleLogs).OrderBy(o => o.Occurred).ToList();
            return (combinedLogs, podLogs.NextSequenceNumber);

            async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber, int? ExitCode)> ReadPodLogs(Stream stream)
            {
                using (var reader = new StreamReader(stream))
                {
                    return await PodLogReader.ReadPodLogs(lastLogSequence, reader);
                }
            }
        }


        async Task<Stream?> GetLogStream(string podName, DateTimeOffset? sinceTime, CancellationToken cancellationToken)
        {
            return await retryPolicy.ExecuteAsync(async ct => await QueryLogs(), cancellationToken);

            async Task<Stream?> QueryLogs()
            {
                try
                {
                    return await Client.GetNamespacedPodLogsAsync(podName, KubernetesConfig.Namespace, podName, sinceTime, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException ex)
                {
                    //Pod logs aren't ready yet
                    if (ex.Response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                    {
                        return null;
                    }

                    throw;
                }
            }
        }
    }
}