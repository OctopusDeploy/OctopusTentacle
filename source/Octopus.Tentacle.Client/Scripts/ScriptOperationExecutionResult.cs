using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Contracts;
using System.Collections.Generic;

namespace Octopus.Tentacle.Client.Scripts
{
    public class ScriptOperationExecutionResult
    {
        public ScriptStatus ScriptStatus { get; }
        public CommandContext ContextForNextCommand { get; }

        public ScriptOperationExecutionResult(ScriptStatus scriptStatus, CommandContext contextForNextCommand)
        {
            ScriptStatus = scriptStatus;
            ContextForNextCommand = contextForNextCommand;
        }

        /// <summary>
        /// Create a result object for when we have most likely started a script, but cancellation has started, and we want to wait for
        /// this script to finish.
        /// </summary>
        internal static ScriptOperationExecutionResult CreateScriptStartedResult(ScriptTicket scriptTicket, ScriptServiceVersion scripServiceVersionUsed)
        {
            var scriptStatus = new ScriptStatus(ProcessState.Pending, 0, new List<ProcessOutput>());
            var contextForNextCommand = new CommandContext(scriptTicket, 0, scripServiceVersionUsed);
            return new(scriptStatus, contextForNextCommand);
        }
    }
}