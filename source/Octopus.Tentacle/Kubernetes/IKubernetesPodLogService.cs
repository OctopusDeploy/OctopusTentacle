using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public class ScriptPodResources
    {
        ConcurrentDictionary<ScriptTicket, DateTimeOffset?> sinceTimes = new ConcurrentDictionary<ScriptTicket, DateTimeOffset?>();
        public void Create(ScriptTicket scriptTicket)
        {
            sinceTimes.GetOrAdd(scriptTicket, _ => null);
        }

        void Delete()
        {
        }

// disposable
        //public ISinceTimeStore GetTimeProvider(ScriptTicket scriptTicket);
        
        public DateTimeOffset? GetSinceTime(ScriptTicket scriptTicket)
        {
            return sinceTimes[scriptTicket];
        }

        public void UpdateSinceTime(ScriptTicket scriptTicket, DateTimeOffset nextSinceTime)
        {
            sinceTimes[scriptTicket] = nextSinceTime;
        }
    }

    public interface IKubernetesPodLogService
    {
        Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken);
    }

    class KubernetesPodLogService : KubernetesService, IKubernetesPodLogService
    {
        readonly IKubernetesPodMonitor podMonitor;
        readonly ITentacleScriptLogProvider scriptLogProvider;
        readonly ScriptPodResources scriptPodResources;

        public KubernetesPodLogService(IKubernetesClientConfigProvider configProvider, IKubernetesPodMonitor podMonitor, ITentacleScriptLogProvider scriptLogProvider, ScriptPodResources scriptPodResources) 
            : base(configProvider)
        {
            this.podMonitor = podMonitor;
            this.scriptLogProvider = scriptLogProvider;
            this.scriptPodResources = scriptPodResources;
        }

        public async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken)
        {
            var tentacleScriptLog = scriptLogProvider.GetOrCreate(scriptTicket);
            var podName = scriptTicket.ToKubernetesScriptPobName();
            var sinceTime = scriptPodResources.GetSinceTime(scriptTicket);

            Stream logStream;

            try
            {
                //TODO: Only grab recent
                //TODO: Add retries
                logStream = await Client.GetNamespacedPodLogsAsync(podName, KubernetesConfig.Namespace, podName, sinceTime, cancellationToken: cancellationToken);
            }
            catch (HttpOperationException ex)
            {
                //Pod logs aren't ready yet
                if (ex.Response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                {
                    return (new List<ProcessOutput>(), lastLogSequence);
                }

                throw;
            }

            var podLogs = await ReadPodLogs(logStream);

            if (podLogs.Outputs.Any())
            {
                var nextSinceTime = podLogs.Outputs.Max(o => o.Occurred);
                //log.Verbose("K8s Next SinceTime: " + nextSinceTime);
                scriptPodResources.UpdateSinceTime(scriptTicket, nextSinceTime);
                
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
    }
}