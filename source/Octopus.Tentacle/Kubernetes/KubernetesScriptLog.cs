using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using k8s.Autorest;
using Newtonsoft.Json;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes.Scripts;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesScriptLog : IScriptLog
    {
        readonly IKubernetesLogService kubernetesLogService;
        readonly SensitiveValueMasker sensitiveValueMasker;
        readonly ScriptTicket scriptTicket;
        readonly object sync = new object();

        readonly List<ProcessOutput> inMemoryTentacleLogs = new List<ProcessOutput>();
        
        public KubernetesScriptLog(IKubernetesLogService kubernetesLogService, SensitiveValueMasker sensitiveValueMasker, ScriptTicket scriptTicket)
        {
            this.kubernetesLogService = kubernetesLogService;
            this.sensitiveValueMasker = sensitiveValueMasker;
            this.scriptTicket = scriptTicket;
        }

        public IScriptLogWriter CreateWriter()
        {
            return new KubernetesWriter(sync, sensitiveValueMasker, inMemoryTentacleLogs);
        }

        public List<ProcessOutput> GetOutput(long afterSequenceNumber, out long nextSequenceNumber)
        {
            var podName = scriptTicket.ToKubernetesScriptPodName();
            
            Stream logStream;

            var writer = CreateWriter();
            try
            {
                //TODO: Only grab recent
                logStream = kubernetesLogService.GetLogs(podName, KubernetesConfig.Namespace, podName).Result;
            }
            catch (AggregateException ex)
            {
                //writer.WriteOutput(ProcessOutputSource.Debug, "ABC123: " + ex.ToString());

                if (ex.InnerExceptions.Single() is HttpOperationException httpOperationException && 
                    httpOperationException.Response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                {
                    nextSequenceNumber = afterSequenceNumber;
                    return new List<ProcessOutput>();
                    
                }

                throw;
            }

            var results = new List<ProcessOutput>();
            nextSequenceNumber = afterSequenceNumber;
            lock (sync)
            {
                using (var reader = new StreamReader(logStream))
                {
                    while (true)
                    {
                        var line = reader.ReadLineAsync().Result;
                        if (line.IsNullOrEmpty())
                        {
                            var output = results.Concat(inMemoryTentacleLogs).OrderBy(m => m.Occurred).ToList();
                            
                            inMemoryTentacleLogs.Clear();
                            return output;
                        }

                        var parsedLine = PodLogParser.ParseLine(writer, line!);
                        if (parsedLine != null)
                        {
                            if (parsedLine.LineNumber > afterSequenceNumber)
                                results.Add(new ProcessOutput(parsedLine.Source, parsedLine.Message, parsedLine.Occurred));
                            
                            nextSequenceNumber = parsedLine.LineNumber;
                        }
                    }
                }
            }
        }

        class KubernetesWriter : IScriptLogWriter
        {
            readonly object sync;
            readonly SensitiveValueMasker sensitiveValueMasker;
            readonly List<ProcessOutput> processOutputs;

            public KubernetesWriter(object sync, SensitiveValueMasker sensitiveValueMasker, List<ProcessOutput> processOutputs)
            {
                this.sync = sync;
                this.sensitiveValueMasker = sensitiveValueMasker;
                this.processOutputs = processOutputs;
            }

            public void WriteOutput(ProcessOutputSource source, string message)
            => WriteOutput(source, message, DateTimeOffset.UtcNow);

            public void WriteOutput(ProcessOutputSource source, string message, DateTimeOffset occurred)
            {
                lock (sync)
                {
                    var output = new ProcessOutput(source, MaskSensitiveValues(message), occurred);
                    processOutputs.Add(output);
                }
            }

            string MaskSensitiveValues(string rawMessage)
            {
                string? maskedMessage = null;
                sensitiveValueMasker.SafeSanitize(rawMessage, s => maskedMessage = s);
                return maskedMessage ?? rawMessage;
            }

            public void Dispose()
            {
            }
        }
    }
}
