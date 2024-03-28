using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha
{
    public class StartKubernetesScriptCommandV1Alpha
    {
        public StartKubernetesScriptCommandV1Alpha(
            ScriptTicket scriptTicket,
            string taskId,
            string scriptBody,
            string[] arguments,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            PodImageConfiguration? podImageConfiguration,
            Dictionary<ScriptType, string>? additionalScripts,
            ScriptFile[]? additionalFiles)
        {
            Arguments = arguments;
            TaskId = taskId;
            ScriptTicket = scriptTicket;
            ScriptBody = scriptBody;
            Isolation = isolation;
            ScriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            IsolationMutexName = isolationMutexName;
            PodImageConfiguration = podImageConfiguration;

            if (additionalFiles != null)
                Files.AddRange(additionalFiles);

            if (additionalScripts == null || !additionalScripts.Any())
                return;

            foreach (var additionalScript in additionalScripts)
            {
                Scripts.Add(additionalScript.Key, additionalScript.Value);
            }
        }

        public string ScriptBody { get; }
        public string TaskId { get; }
        public ScriptTicket ScriptTicket { get; }

        public ScriptIsolationLevel Isolation { get; }
        public TimeSpan ScriptIsolationMutexTimeout { get; }
        public string? IsolationMutexName { get; }
        public PodImageConfiguration? PodImageConfiguration { get; }

        public Dictionary<ScriptType, string> Scripts { get; } = new();
        public List<ScriptFile> Files { get; } = new();
        public string[] Arguments { get; }
    }
}