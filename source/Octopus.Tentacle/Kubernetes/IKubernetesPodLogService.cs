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
            var podEventsTask = GetPodEvents(scriptTicket, podName, cancellationToken);
            await Task.WhenAll(podLogsTask, podEventsTask);

            var podLogs = await podLogsTask;
            if (podLogs.Outputs.Any())
            {
                var nextSinceTime = podLogs.Outputs.Max(o => o.Occurred);
                scriptPodSinceTimeStore.UpdatePodLogsSinceTime(scriptTicket, nextSinceTime);

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
                .OrderBy(o => o.Occurred)
                .SelectMany(o => o switch
                {
                    //if this is a wrapped output, expand it in place
                    WrappedProcessOutput wrappedOutput => wrappedOutput.Expand(),
                    _ => new[] { o }
                })
                .ToList();

            return (combinedLogs, podLogs.NextSequenceNumber);

            async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber, int? ExitCode)> GetPodLogs()
            {
                var sinceTime = scriptPodSinceTimeStore.GetPodLogsSinceTime(scriptTicket);
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
            var sinceTime = scriptPodSinceTimeStore.GetPodEventsSinceTime(scriptTicket);

            var allEvents = await eventService.FetchAllEventsAsync(KubernetesConfig.Namespace, podName, cancellationToken);
            if (allEvents is null)
            {
                return Array.Empty<ProcessOutput>();
            }

            var relevantEvents = allEvents.Items
                .Select(e => (Event: e, Occurred: EventHelpers.GetLatestTimestampInEvent(e)))
                .Where(x => x.Occurred.HasValue)
                .Select(x => (x.Event, Occurred: new DateTimeOffset(x.Occurred!.Value, TimeSpan.Zero)))
                .OrderBy(x => x.Occurred)
                .SkipWhile(e => e.Occurred <= sinceTime);

            var events = relevantEvents.Select((x) =>
                {
                    var (ev, occurred) = x;

                    var formattedMessage = $"[POD EVENT] {ev.Reason} | {ev.Message} (Count: {ev.Series?.Count ?? 1})";

                    return ev.Type.Equals("Warning", StringComparison.OrdinalIgnoreCase) 
                        ? new WrappedProcessOutput(ProcessOutputSource.StdOut, formattedMessage, occurred, "warning") 
                        : new ProcessOutput(ProcessOutputSource.Debug, formattedMessage, occurred);
                    
                })
                .ToArray();

            if (events.Any())
            {
                //update the events since time, so we don't get duplicate events
                scriptPodSinceTimeStore.UpdatePodEventsSinceTime(scriptTicket, events.Max(o => o.Occurred));
            }

            return events;
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

        class WrappedProcessOutput : ProcessOutput
        {
            static readonly TimeSpan OneTick = TimeSpan.FromTicks(1);
            readonly string wrapper;

            public WrappedProcessOutput(ProcessOutputSource source, string text, DateTimeOffset occurred, string wrapper)
                : base(source, text, occurred)
            {
                this.wrapper = wrapper;
            }

            public IEnumerable<ProcessOutput> Expand()
            {
                return new[]
                {
                    //we add the service messages one tick before and after so they are correctly formatted
                    new ProcessOutput(ProcessOutputSource.StdOut, $"##octopus[stdout-{wrapper}]", Occurred.Subtract(OneTick)),
                    new ProcessOutput(Source, Text, Occurred),
                    new ProcessOutput(ProcessOutputSource.StdOut, "##octopus[stdout-default]", Occurred.Add(OneTick)),
                };
            }
        }
    }
}