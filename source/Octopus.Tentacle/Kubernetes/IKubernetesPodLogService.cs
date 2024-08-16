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
        readonly IKubernetesEventService eventService;

        public KubernetesPodLogService(
            IKubernetesClientConfigProvider configProvider, 
            IKubernetesPodMonitor podMonitor,
            ITentacleScriptLogProvider scriptLogProvider,
            IScriptPodSinceTimeStore scriptPodSinceTimeStore, 
            IKubernetesEventService eventService,
            ISystemLog log) 
            : base(configProvider, log)
        {
            this.podMonitor = podMonitor;
            this.scriptLogProvider = scriptLogProvider;
            this.scriptPodSinceTimeStore = scriptPodSinceTimeStore;
            this.eventService = eventService;
        }

        public async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken)
        {
            var tentacleScriptLog = scriptLogProvider.GetOrCreate(scriptTicket);
            var podName = scriptTicket.ToKubernetesScriptPodName();

            var podLogsTask = GetPodLogs();
            var podEventsTask = GetPodEvents(scriptTicket, podName,  cancellationToken);
            await Task.WhenAll(podLogsTask, podEventsTask);
            
            var podLogs = await podLogsTask;
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
            var podEventLogs = await podEventsTask;
            
            var combinedLogs = podLogs
                .Outputs
                .Concat(tentacleLogs)
                .Concat(podEventLogs)
                .OrderBy(o => o.Occurred).ToList();
            
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
                using var reader = new StreamReader(stream);
                return await PodLogReader.ReadPodLogs(lastLogSequence, reader);
            }
        }

        async Task<IEnumerable<ProcessOutput>> GetPodEvents(ScriptTicket scriptTicket, string podName, CancellationToken cancellationToken)
        {
            var sinceTime = scriptPodSinceTimeStore.GetSinceTime(scriptTicket);

            var allEvents = await eventService.FetchAllEventsAsync(KubernetesConfig.Namespace, podName, cancellationToken);
            if (allEvents is null)
            {
                return Array.Empty<ProcessOutput>();
            }

            var relevantEvents = allEvents.Items
                .Select(e=> (e, EventHelpers.GetEarliestTimestampInEvent(e)))
                .Where(x => x.Item2.HasValue)
                .Select(x => (x.e, new DateTimeOffset(x.Item2!.Value, TimeSpan.Zero)))
                .OrderBy(x => x.Item2)
                .SkipWhile(e => e.Item2 < sinceTime);

            return relevantEvents.Select((x) =>
                {
                    var (ev, occurred) = x;
                    return new ProcessOutput(ProcessOutputSource.Debug, $"{ev.Reason} - {ev.Message}", occurred);
                })
                .ToArray();
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