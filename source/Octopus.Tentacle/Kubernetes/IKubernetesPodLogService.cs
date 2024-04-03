using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodLogService
    {
        Task<(IReadOnlyCollection<ProcessOutput>, long)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken);

        IKubernetesInMemoryLogWriter CreateWriter(ScriptTicket scriptTicket);
    }

    class KubernetesPodLogService : KubernetesService, IKubernetesPodLogService
    {
        public KubernetesPodLogService(IKubernetesClientConfigProvider configProvider) : base(configProvider)
        {
        }

        public async Task<(IReadOnlyCollection<ProcessOutput>, long)> GetLogs(ScriptTicket scriptTicket, long lastLogSequence, CancellationToken cancellationToken)
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
            
            using (var reader = new StreamReader(logStream))
            {
                return await ValueTuple(lastLogSequence, async () => await reader.ReadLineAsync());
            }
            

        }

        static async Task<(IReadOnlyCollection<ProcessOutput>, long)> ValueTuple(long lastLogSequence, Func<Task<string?>> readLine)
        {
            var results = new List<ProcessOutput>();
            var nextSequenceNumber = lastLogSequence;

            while (true)
            {
                var line = await readLine();

                //No more to read
                if (line.IsNullOrEmpty())
                {
                    return (results, nextSequenceNumber);
                }

                //TODO: print parse errors
                //TODO: detect missing line
                var parseResult = PodLogParser.ParseLine(line!);
                var podLogLine = parseResult.LogLine;
                if (podLogLine != null && podLogLine.LineNumber > lastLogSequence)
                {
                    results.Add(new ProcessOutput(podLogLine.Source, podLogLine.Message, podLogLine.Occurred));
                    nextSequenceNumber = podLogLine.LineNumber;
                }

                //TODO: try not to read any more if we see a panic?
                    
            }
        }

        public IKubernetesInMemoryLogWriter CreateWriter(ScriptTicket scriptTicket)
        {
            throw new System.NotImplementedException();
        }
    }

    public interface IKubernetesInMemoryLogWriter
    {
        void WriteVerbose(ScriptTicket scriptTicket, string message);
        void WriteError(ScriptTicket scriptTicket, string message);
        void WriteInfo(ScriptTicket scriptTicket, string message);
    }
}