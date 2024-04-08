using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s.Autorest;
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

        public KubernetesPodLogService(IKubernetesClientConfigProvider configProvider, IKubernetesPodMonitor podMonitor) : base(configProvider)
        {
            this.podMonitor = podMonitor;
        }

        public async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken)
        {
            var podName = scriptTicket.ToKubernetesScriptPobName();
            DateTimeOffset? sinceTime = null;

            Stream logStream;

            try
            {
                //TODO: Only grab recent
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

            var podLogs = await ReadPodLogs();

            //We can use our EOS marker to detect completion quicker than the Pod status
            if (podLogs.ExitCode != null)
                podMonitor.MarkAsCompleted(scriptTicket, podLogs.ExitCode.Value);

            return (podLogs.Outputs, podLogs.NextSequenceNumber);

            async Task<(IReadOnlyCollection<ProcessOutput> Outputs, long NextSequenceNumber, int? ExitCode)> ReadPodLogs()
            {
                using (var reader = new StreamReader(logStream))
                {
                    return await PodLogReader.ReadPodLogs(lastLogSequence, reader);
                }
            }
        }
    }
}