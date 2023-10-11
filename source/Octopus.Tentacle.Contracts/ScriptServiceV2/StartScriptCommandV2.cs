using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.ScriptServiceV2
{
    public class StartScriptCommandV2
    {
        [JsonConstructor]
        public StartScriptCommandV2(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string? isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,
            TimeSpan? durationToWaitForScriptToFinish,
            Dictionary<string,string>? executionProperties)
        {
            Arguments = arguments;
            TaskId = taskId;
            ScriptTicket = scriptTicket;
            DurationToWaitForScriptToFinish = durationToWaitForScriptToFinish;
            ExecutionProperties = executionProperties;
            ScriptBody = scriptBody;
            Isolation = isolation;
            ScriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            IsolationMutexName = isolationMutexName;
        }

        public StartScriptCommandV2(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,
            TimeSpan? durationToWaitForScriptToFinish,
            Dictionary<string,string>? executionProperties,
            params ScriptFile[]? additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId,
                scriptTicket,
                durationToWaitForScriptToFinish,
                executionProperties)
        {
            if (additionalFiles != null)
                Files.AddRange(additionalFiles);
        }

        public StartScriptCommandV2(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,
            TimeSpan? durationToWaitForScriptToFinish,
            Dictionary<string,string>? executionProperties,
            Dictionary<ScriptType, string>? additionalScripts,
            params ScriptFile[]? additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId,
                scriptTicket,
                durationToWaitForScriptToFinish,
                executionProperties,
                additionalFiles)
        {
            if (additionalScripts == null || !additionalScripts.Any())
                return;

            foreach (var additionalScript in additionalScripts)
            {
                Scripts.Add(additionalScript.Key, additionalScript.Value);
            }
        }

        public ScriptTicket ScriptTicket { get; set; }
        public string ScriptBody { get; }
        public string TaskId { get; }
        public TimeSpan? DurationToWaitForScriptToFinish { get; }

        public ScriptIsolationLevel Isolation { get; }
        public TimeSpan ScriptIsolationMutexTimeout { get; }
        public string? IsolationMutexName { get; }

        public Dictionary<ScriptType, string> Scripts { get; } = new();
        public List<ScriptFile> Files { get; } = new();
        public string[] Arguments { get; }

        public Dictionary<string, string>? ExecutionProperties { get; }
    }
}