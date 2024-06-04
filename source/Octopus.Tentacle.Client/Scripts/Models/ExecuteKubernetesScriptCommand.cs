using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public class ExecuteKubernetesScriptCommand : ExecuteScriptCommand
    {
        public KubernetesImageConfiguration? ImageConfiguration { get; }

        public string? ScriptPodServiceAccountName { get;}

        public bool IsRawScriptWithNoDependencies { get; }

        public ExecuteKubernetesScriptCommand(
            ScriptTicket scriptTicket,
            string taskId,
            string scriptBody,
            string[] arguments,
            ScriptIsolationConfiguration isolationConfiguration,
            Dictionary<ScriptType, string>? additionalScripts,
            ScriptFile[] additionalFiles,
            KubernetesImageConfiguration? imageConfiguration, 
            string? scriptPodServiceAccountName,
            bool isRawScriptWithNoDependencies)
            : base(scriptTicket, taskId, scriptBody, arguments, isolationConfiguration, additionalScripts, additionalFiles)
        {
            ImageConfiguration = imageConfiguration;
            ScriptPodServiceAccountName = scriptPodServiceAccountName;
            IsRawScriptWithNoDependencies = isRawScriptWithNoDependencies;
        }
    }
}