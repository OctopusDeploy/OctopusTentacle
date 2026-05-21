using System;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;

namespace Octopus.Tentacle.Client.Scripts.Models.Builders
{
    public class ExecuteKubernetesScriptCommandBuilder : ExecuteScriptCommandBuilder
    {
        KubernetesImageConfiguration? imageConfiguration;
        string? scriptPodServiceAccountName;
        string? scriptPodPlatform;
        bool isRawScript;
        KubernetesAgentAuthContext? authContext;
        CalamariImageConfiguration? calamariImageConfiguration;

        public ExecuteKubernetesScriptCommandBuilder(string taskId)
            : base(taskId, ScriptIsolationLevel.NoIsolation) //Kubernetes Agents don't need isolation since the scripts won't clash with each other (it won't clash more than Workers anyway)
        {
        }

        public ExecuteKubernetesScriptCommandBuilder WithKubernetesImageConfiguration(KubernetesImageConfiguration configuration)
        {
            this.imageConfiguration = configuration;
            return this;
        }

        public ExecuteKubernetesScriptCommandBuilder WithScriptPodServiceAccountName(string serviceAccountName)
        {
            scriptPodServiceAccountName = serviceAccountName;
            return this;
        }

        public ExecuteKubernetesScriptCommandBuilder IsRawScript()
        {
            isRawScript = true;
            return this;
        }

        public ExecuteKubernetesScriptCommandBuilder WithAuthContext(KubernetesAgentAuthContext context)
        {
            authContext = context;
            return this;
        }

        public ExecuteKubernetesScriptCommandBuilder WithScriptPodPlatform(string scriptPodPlatform)
        {
            this.scriptPodPlatform = scriptPodPlatform;
            return this;
        }

        public ExecuteKubernetesScriptCommandBuilder WithCalamariImageConfiguration(CalamariImageConfiguration configuration)
        {
            calamariImageConfiguration = configuration;
            return this;
        }

        public override ExecuteScriptCommand Build()
            => new ExecuteKubernetesScriptCommand(
                ScriptTicket,
                TaskId,
                ScriptBody.ToString(),
                Arguments.ToArray(),
                IsolationConfiguration,
                AdditionalScripts,
                Files.ToArray(),
                imageConfiguration,
                scriptPodServiceAccountName,
                isRawScript,
                authContext,
                scriptPodPlatform,
                calamariImageConfiguration
            );
    }
}