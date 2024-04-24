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

        public KubernetesPodLogService(IKubernetesClientConfigProvider configProvider, IKubernetesPodMonitor podMonitor, ITentacleScriptLogProvider scriptLogProvider, IScriptPodSinceTimeStore scriptPodSinceTimeStore, ISystemLog log) 
            : base(configProvider, log)
        {
            this.podMonitor = podMonitor;
            this.scriptLogProvider = scriptLogProvider;
            this.scriptPodSinceTimeStore = scriptPodSinceTimeStore;
        }

        public async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken)
        {
            var tentacleScriptLog = scriptLogProvider.GetOrCreate(scriptTicket);
            var podName = scriptTicket.ToKubernetesScriptPodName();

            var podLogs = await GetPodLogs();
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

            async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber, int? ExitCode)> GetPodLogs()
            {
                var sinceTime = scriptPodSinceTimeStore.GetSinceTime(scriptTicket);
                try
                {
                    return await GetPodLogsWithSinceTime(sinceTime);
                }
                catch (UnexpectedPodLogLineNumberException ex)
                {
                    var message = $"Unexpected Pod log line numbers found with sinceTime='{sinceTime}', loading all logs";
                    tentacleScriptLog.Verbose(message);
                    Log.Warn(ex, message);
                    
                    //If we somehow come across weird/missing line numbers, try load the whole Pod logs to see if that helps
                    return await GetPodLogsWithSinceTime(null);
                }
            }

            async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber, int? ExitCode)> GetPodLogsWithSinceTime(DateTimeOffset? sinceTime)
            {
                var logStream = await GetLogStream(podName, sinceTime, cancellationToken: cancellationToken);
                return logStream != null ? await ReadPodLogsFromStream(logStream) : (new List<ProcessOutput>(), lastLogSequence, null);
            }

            async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber, int? ExitCode)> ReadPodLogsFromStream(Stream stream)
            {
                using (var reader = new StreamReader(stream))
                {
                    return await PodLogReader.ReadPodLogs(lastLogSequence, reader);
                }
            }
        }


        async Task<Stream?> GetLogStream(string podName, DateTimeOffset? sinceTime, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(async ct => await QueryLogs(), cancellationToken);

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