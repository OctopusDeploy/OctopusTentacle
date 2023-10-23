using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class StartScriptCommandV3Alpha : ScriptCommandV3Alpha
    {
        [JsonConstructor]
        public StartScriptCommandV3Alpha(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string? isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,
            TimeSpan? durationToWaitForScriptToFinish,
            IScriptExecutionContext scriptExecutionContext)
            : base(scriptTicket)
        {
            Arguments = arguments;
            TaskId = taskId;
            DurationToWaitForScriptToFinish = durationToWaitForScriptToFinish;
            ScriptExecutionContext = scriptExecutionContext;
            ScriptBody = scriptBody;
            Isolation = isolation;
            ScriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            IsolationMutexName = isolationMutexName;
        }

        public StartScriptCommandV3Alpha(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,
            TimeSpan? durationToWaitForScriptToFinish,
            IScriptExecutionContext scriptExecutionContext,
            params ScriptFile[]? additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId,
                scriptTicket,
                durationToWaitForScriptToFinish,
                scriptExecutionContext)
        {
            if (additionalFiles != null)
                Files.AddRange(additionalFiles);
        }

        public StartScriptCommandV3Alpha(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,
            TimeSpan? durationToWaitForScriptToFinish,
            IScriptExecutionContext scriptExecutionContext,
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
                scriptExecutionContext,
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
        public string TaskId { get; }
        public TimeSpan? DurationToWaitForScriptToFinish { get; }
        public IScriptExecutionContext ScriptExecutionContext { get; }

        public ScriptIsolationLevel Isolation { get; }
        public TimeSpan ScriptIsolationMutexTimeout { get; }
        public string? IsolationMutexName { get; }

        public Dictionary<ScriptType, string> Scripts { get; } = new();
        public List<ScriptFile> Files { get; } = new();
        public string[] Arguments { get; }
    }
}