using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Diagnostics;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Client
{
    public interface ITentacleClient : IDisposable
    {
        Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ITentacleClientTaskLog logger, CancellationToken cancellationToken);
        Task<DataStream?> DownloadFile(string remotePath, ITentacleClientTaskLog logger, CancellationToken cancellationToken);

        /// <summary>
        /// Execute a script on Tentacle in its entirety. 
        /// </summary>
        /// <param name="executeScriptCommand">The execute script command</param>
        /// <param name="onScriptStatusResponseReceived"></param>
        /// <param name="onScriptCompleted">Called when the script has finished executing on Tentacle but before the working directory is cleaned up.
        /// This is called regardless of the outcome of the script. It will also be called if the script execution is cancelled.</param>
        /// <param name="logger">Used to output user orientated log messages</param>
        /// <param name="scriptExecutionCancellationToken">When cancelled, will attempt to stop the execution of the script on Tentacle before returning.</param>
        /// <returns></returns>
        Task<ScriptExecutionResult> ExecuteScript(
            ExecuteScriptCommand executeScriptCommand,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            ITentacleClientTaskLog logger,
            CancellationToken scriptExecutionCancellationToken);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="startScriptIsBeingReAttempted"></param>
        /// <param name="logger"></param>
        /// <param name="requestCancellationToken">Cancels the inflight request</param>
        /// <returns>The result, which includes the CommandContext for the next command</returns>
        Task<ScriptOperationExecutionResult> StartScript(ExecuteScriptCommand command,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            ITentacleClientTaskLog logger,
            CancellationToken requestCancellationToken);
        

        /// <summary>
        /// Get the status.
        /// </summary>
        /// <param name="commandContext">The CommandContext from the previous command</param>
        /// <param name="logger"></param>
        /// <param name="requestCancellationToken">Cancels the inflight request</param>
        /// <returns>The result, which includes the CommandContext for the next command</returns>
        Task<ScriptOperationExecutionResult> GetStatus(CommandContext commandContext, ITentacleClientTaskLog logger, CancellationToken requestCancellationToken);

        /// <summary>
        /// Cancel the script.
        /// </summary>
        /// <param name="commandContext">The CommandContext from the previous command</param>
        /// <param name="logger"></param>
        /// <param name="requestCancellationToken">Cancels the inflight request</param>
        /// <returns>The result, which includes the CommandContext for the next command</returns>
        Task<ScriptOperationExecutionResult> CancelScript(CommandContext commandContext, ITentacleClientTaskLog logger, CancellationToken requestCancellationToken);

        /// <summary>
        /// Complete the script.
        /// </summary>
        /// <param name="commandContext">The CommandContext from the previous command</param>
        /// <param name="logger"></param>
        /// <param name="requestCancellationToken">Cancels the inflight request</param>
        Task<ScriptStatus?> CompleteScript(CommandContext commandContext, ITentacleClientTaskLog logger, CancellationToken requestCancellationToken);
    }
}