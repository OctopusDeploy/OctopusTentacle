using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;
using Octopus.Shared.Util;

namespace Octopus.Shared.Contracts
{
    public class StartScriptCommand
    {
        [JsonConstructor]
        public StartScriptCommand(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string? taskId)
        {
            Arguments = arguments;
            TaskId = taskId;
            ScriptBody = scriptBody;
            Isolation = isolation;
            ScriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            IsolationMutexName = isolationMutexName;
        }

        public StartScriptCommand(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string? taskId,
            params ScriptFile[] additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId)
        {
            if (additionalFiles != null)
                Files.AddRange(additionalFiles);
        }

        public StartScriptCommand(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string? taskId,
            Dictionary<ScriptType, string> additionalScripts,
            params ScriptFile[] additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId,
                additionalFiles)
        {
            if (!additionalScripts.IsNullOrEmpty())
                Scripts.AddRange(additionalScripts);
        }

        public string ScriptBody { get; }

        public ScriptIsolationLevel Isolation { get; }

        public Dictionary<ScriptType, string> Scripts { get; } = new Dictionary<ScriptType, string>();

        public List<ScriptFile> Files { get; } = new List<ScriptFile>();

        public string[] Arguments { get; }

        public string? TaskId { get; }

        public TimeSpan ScriptIsolationMutexTimeout { get; }
        public string IsolationMutexName { get; }
    }
}