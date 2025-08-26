using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public class ExecuteKubernetesScriptCommand : ExecuteScriptCommand
    {
        public KubernetesImageConfiguration? ImageConfiguration { get; }

        public string? ScriptPodServiceAccountName { get;}

        public bool IsRawScript { get; }

        public KubernetesAgentAuthContext? AuthContext { get; }

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
            bool isRawScript,
            KubernetesAgentAuthContext? authContext)
            : base(scriptTicket, taskId, scriptBody, arguments, isolationConfiguration, additionalScripts, additionalFiles)
        {
            ImageConfiguration = imageConfiguration;
            ScriptPodServiceAccountName = scriptPodServiceAccountName;
            IsRawScript = isRawScript;
            AuthContext = authContext;
        }
    }
}