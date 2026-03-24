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
            TimeSpan? durationToWaitForScriptToFinish = null,
            TimeSpan? durationToWaitForPowerShellToStart = null)
            : base(scriptTicket, taskId, scriptBody, arguments, isolationConfiguration, additionalScripts, additionalFiles)
        {
            DurationToWaitForScriptToFinish = durationToWaitForScriptToFinish;
            DurationToWaitForPowerShellToStart = durationToWaitForPowerShellToStart;
        }

        public TimeSpan? DurationToWaitForScriptToFinish { get;  }
        public TimeSpan? DurationToWaitForPowerShellToStart { get; }
    }
}