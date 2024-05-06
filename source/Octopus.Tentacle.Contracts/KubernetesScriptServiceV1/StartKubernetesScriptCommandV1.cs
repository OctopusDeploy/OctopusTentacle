using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1
{
    public class StartKubernetesScriptCommandV1
    {
        public StartKubernetesScriptCommandV1(
            ScriptTicket scriptTicket,
            string taskId,
            string scriptBody,
            string[] arguments,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            PodImageConfigurationV1? podImageConfiguration, 
            string? scriptPodServiceAccountName,
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
            ScriptPodServiceAccountName = scriptPodServiceAccountName;

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
        public string IsolationMutexName { get; }
        public PodImageConfigurationV1? PodImageConfiguration { get; }

        public Dictionary<ScriptType, string> Scripts { get; } = new();
        public List<ScriptFile> Files { get; } = new();
        public string[] Arguments { get; }

        public string? ScriptPodServiceAccountName { get; }
    }
}
