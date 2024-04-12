using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models.Builders
{
    public class ExecuteKubernetesScriptCommandBuilder : ExecuteScriptCommandBuilder
    {
        KubernetesImageConfiguration? configuration;
        string? scriptPodServiceAccountName;

        public ExecuteKubernetesScriptCommandBuilder(string taskId)
            : base(taskId, ScriptIsolationLevel.NoIsolation) //Kubernetes Agents don't need isolation since the scripts won't clash with each other (it won't clash more than Workers anyway)
        {
        }

        public ExecuteKubernetesScriptCommandBuilder WithKubernetesImageConfiguration(KubernetesImageConfiguration configuration)
        {
            this.configuration = configuration;
            return this;
        }

        public ExecuteKubernetesScriptCommandBuilder WithScriptPodServiceAccountName(string serviceAccountName)
        {
            scriptPodServiceAccountName = serviceAccountName;
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
                configuration,
                scriptPodServiceAccountName
            );
    }
}