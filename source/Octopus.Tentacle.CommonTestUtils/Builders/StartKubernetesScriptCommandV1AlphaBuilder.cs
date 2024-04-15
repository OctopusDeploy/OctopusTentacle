using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Builders;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;

namespace Octopus.Tentacle.CommonTestUtils.Builders
{
    public class StartKubernetesScriptCommandV1AlphaBuilder
    {
        readonly List<ScriptFile> files = new();
        readonly List<string> arguments = new();
        readonly Dictionary<ScriptType, string> additionalScripts = new();
        StringBuilder scriptBody = new(string.Empty);
        ScriptIsolationLevel isolation = ScriptIsolationLevel.NoIsolation;
        TimeSpan scriptIsolationMutexTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
        string scriptIsolationMutexName = "RunningScript";
        string taskId = Guid.NewGuid().ToString();
        ScriptTicket scriptTicket = new UniqueScriptTicketBuilder().Build();
        PodImageConfiguration? podImageConfiguration = null;
        string? scriptPodServiceAccountName;

        public StartKubernetesScriptCommandV1AlphaBuilder WithScriptBody(string scriptBody)
        {
            this.scriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithAdditionalScriptTypes(ScriptType scriptType, string scriptBody)
        {
            additionalScripts.Add(scriptType, scriptBody);
            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithIsolation(ScriptIsolationLevel isolation)
        {
            this.isolation = isolation;
            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithFiles(params ScriptFile[] files)
        {
            if (files != null)
                this.files.AddRange(files);

            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithArguments(params string[] arguments)
        {
            if (arguments != null)
                this.arguments.AddRange(arguments);

            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            this.scriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithMutexName(string name)
        {
            scriptIsolationMutexName = name;
            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithTaskId(string taskId)
        {
            this.taskId = taskId;
            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithScriptTicket(ScriptTicket scriptTicket)
        {
            this.scriptTicket = scriptTicket;
            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithPodImageConfiguration(PodImageConfiguration imageConfiguration)
        {
            this.podImageConfiguration = imageConfiguration;
            return this;
        }

        public StartKubernetesScriptCommandV1AlphaBuilder WithScriptPodServiceAccountName(string serviceAccountName)
        {
            scriptPodServiceAccountName = serviceAccountName;
            return this;
        }

        public StartKubernetesScriptCommandV1Alpha Build()
            => new(
                scriptTicket,
                taskId,
                scriptBody.ToString(),
                arguments.ToArray(),
                isolation,
                scriptIsolationMutexTimeout,
                scriptIsolationMutexName,
                podImageConfiguration,
                scriptPodServiceAccountName,
                additionalScripts,
                files.ToArray());
    }
}