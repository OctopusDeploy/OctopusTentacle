using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public class ExecuteShellScriptCommand : ExecuteScriptCommand
    {
        public ExecuteShellScriptCommand(
            ScriptTicket scriptTicket,
            string taskId,
            string scriptBody,
            string[] arguments,
            ScriptIsolationConfiguration isolationConfiguration,
            Dictionary<ScriptType, string>? additionalScripts = null,
            ScriptFile[]? additionalFiles = null,
            TimeSpan? durationToWaitForScriptToFinish = null)
            : base(scriptTicket, taskId, scriptBody, arguments, isolationConfiguration, additionalScripts, additionalFiles)
        {
            DurationToWaitForScriptToFinish = durationToWaitForScriptToFinish;
        }

        public TimeSpan? DurationToWaitForScriptToFinish { get;  }
    }
}