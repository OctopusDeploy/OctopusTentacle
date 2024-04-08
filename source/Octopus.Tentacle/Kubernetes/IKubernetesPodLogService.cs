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

        void GetTentacleLogger()
        {
        }

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
        readonly ScriptPodResources scriptPodResources;
        readonly ISystemLog log;
        public KubernetesPodLogService(IKubernetesClientConfigProvider configProvider, IKubernetesPodMonitor podMonitor, ScriptPodResources scriptPodResources, ISystemLog log) : base(configProvider)
        {
            this.podMonitor = podMonitor;
            this.scriptPodResources = scriptPodResources;
            this.log = log;
        }

        public async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken)
        {
            var podName = scriptTicket.ToKubernetesScriptPobName();
            var sinceTime = scriptPodResources.GetSinceTime(scriptTicket);

            Stream logStream;

            try
            {
                logStream = await Client.GetNamespacedPodLogsAsync(log, podName, KubernetesConfig.Namespace, podName, sinceTime, cancellationToken: cancellationToken);
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
                log.Verbose("K8s Next SinceTime: " + nextSinceTime);
                scriptPodResources.UpdateSinceTime(scriptTicket, nextSinceTime);
                
                //We can use our EOS marker to detect completion quicker than the Pod status
                if (podLogs.ExitCode != null)
                    podMonitor.MarkAsCompleted(scriptTicket, podLogs.ExitCode.Value);
            }

            return (podLogs.Outputs, podLogs.NextSequenceNumber);

            async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber, int? ExitCode)> ReadPodLogs(Stream stream)
            {
                using (var reader = new StreamReader(stream))
                {
                    return await PodLogReader.ReadPodLogs(lastLogSequence, reader, log);
                }
            }
        }
    }
}