using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models.Builders
{
    public class ExecuteShellScriptCommandBuilder : ExecuteScriptCommandBuilder
    {
        TimeSpan? durationStartScriptCanWaitForScriptToFinish = TimeSpan.FromSeconds(5); // The UI refreshes every 5 seconds, so 5 seconds here might be reasonable.

        public ExecuteShellScriptCommandBuilder(string taskId, ScriptIsolationLevel defaultIsolationLevel) : base(taskId, defaultIsolationLevel)
        {
        }

        public ExecuteScriptCommandBuilder SetDurationStartScriptCanWaitForScriptToFinish(TimeSpan? duration)
        {
            durationStartScriptCanWaitForScriptToFinish = duration;
            return this;
        }

        public override ExecuteScriptCommand Build()
            => new ExecuteShellScriptCommand(
                ScriptTicket,
                TaskId,
                ScriptBody.ToString(),
                Arguments.ToArray(),
                IsolationConfiguration,
                AdditionalScripts,
                Files.ToArray(),
                durationStartScriptCanWaitForScriptToFinish);
    }
}