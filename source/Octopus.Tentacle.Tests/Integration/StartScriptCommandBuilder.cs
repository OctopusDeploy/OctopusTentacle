using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Tests.Integration
{
    public class StartScriptCommandBuilder
    {
        readonly List<ScriptFile> files = new List<ScriptFile>();
        readonly List<string> arguments = new List<string>();
        readonly Dictionary<ScriptType, string> additionalScripts = new Dictionary<ScriptType, string>();
        StringBuilder scriptBody = new StringBuilder(string.Empty);
        ScriptIsolationLevel isolation = ScriptIsolationLevel.FullIsolation;
        TimeSpan scriptIsolationMutexTimeout = ScriptIsolationMutex.NoTimeout;
        string scriptIsolationMutexName = nameof(RunningScript);
        string? taskId;

        public StartScriptCommandBuilder WithScriptBody(string scriptBody)
        {
            this.scriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public StartScriptCommandBuilder WithAdditionalScriptTypes(ScriptType scriptType, string scriptBody)
        {
            additionalScripts.Add(scriptType, scriptBody);
            return this;
        }

        public StartScriptCommandBuilder WithIsolation(ScriptIsolationLevel isolation)
        {
            this.isolation = isolation;
            return this;
        }

        public StartScriptCommandBuilder WithFiles(params ScriptFile[] files)
        {
            if (files != null)
                this.files.AddRange(files);

            return this;
        }

        public StartScriptCommandBuilder WithArguments(params string[] arguments)
        {
            if (arguments != null)
                this.arguments.AddRange(arguments);

            return this;
        }

        public StartScriptCommandBuilder WithMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            this.scriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            return this;
        }

        public StartScriptCommandBuilder WithMutexName(string name)
        {
            scriptIsolationMutexName = name;
            return this;
        }

        public StartScriptCommandBuilder WithTaskId(string taskId)
        {
            this.taskId = taskId;
            return this;
        }

        public StartScriptCommand Build()
            => new StartScriptCommand(scriptBody.ToString(),
                isolation,
                scriptIsolationMutexTimeout,
                scriptIsolationMutexName,
                arguments.ToArray(),
                taskId,
                additionalScripts,
                files.ToArray());
    }
}