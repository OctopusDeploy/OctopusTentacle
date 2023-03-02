using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts
{
    public class StartScriptCommand
    {
        [JsonConstructor]
        public StartScriptCommand(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string? isolationMutexName,
            string[] arguments,
            string? taskId,
            string? scriptTicketId)
        {
            Arguments = arguments;
            TaskId = taskId;
            ScriptTicketId = scriptTicketId;
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
            string? scriptTicketId,
            params ScriptFile[] additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId,
                scriptTicketId)
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
            string? scriptTicketId,
            Dictionary<ScriptType, string> additionalScripts,
            params ScriptFile[] additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId,
                scriptTicketId,
                additionalFiles)
        {
            if (additionalScripts == null || !additionalScripts.Any())
                return;

            foreach (var additionalScript in additionalScripts)
            {
                Scripts.Add(additionalScript.Key, additionalScript.Value);
            }
        }

        public string ScriptBody { get; }

        public ScriptIsolationLevel Isolation { get; }

        public Dictionary<ScriptType, string> Scripts { get; } = new Dictionary<ScriptType, string>();

        public List<ScriptFile> Files { get; } = new List<ScriptFile>();

        public string[] Arguments { get; }

        public string? TaskId { get; }
        public string? ScriptTicketId { get; }

        public TimeSpan ScriptIsolationMutexTimeout { get; }
        public string? IsolationMutexName { get; }
    }
}