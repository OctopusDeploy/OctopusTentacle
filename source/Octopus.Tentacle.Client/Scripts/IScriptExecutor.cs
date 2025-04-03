using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    interface IScriptExecutor 
    {
        /// <summary>
        /// Start the script.
        /// </summary>
        /// <returns>The result, which includes the CommandContext for the next command</returns>
        Task<ScriptOperationExecutionResult> StartScript(ExecuteScriptCommand command,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken scriptExecutionCancellationToken);

        /// <summary>
        /// Get the status.
        /// </summary>
        /// <param name="commandContext">The CommandContext from the previous command</param>
        /// <param name="scriptExecutionCancellationToken"></param>
        /// <returns>The result, which includes the CommandContext for the next command</returns>
        Task<ScriptOperationExecutionResult> GetStatus(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken);

        /// <summary>
        /// Cancel the script.
        /// </summary>
        /// <param name="commandContext">The CommandContext from the previous command</param>
        /// <returns>The result, which includes the CommandContext for the next command</returns>
        Task<ScriptOperationExecutionResult> CancelScript(CommandContext commandContext, CancellationToken cancellationToken);

        /// <summary>
        /// Complete the script.
        /// </summary>
        /// <param name="commandContext">The CommandContext from the previous command</param>
        /// <param name="scriptExecutionCancellationToken"></param>
        Task<ScriptStatus?> CompleteScript(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
    }
}