using System;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITentacleClientTaskLog logger);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger);

        /// <summary>
        /// When a script is unable to be cancelled for the scriptCancellationTimeoutBeforeAbandoning peroid,
        /// the script will be abandoned and this method will be called.
        /// Passed are the specifics of the script that was abandoned during cancellation.
        /// </summary>
        void ScriptCancellationTimedOut(ScriptCancellationTimedOutEvent scriptCancellationTimedOutEvent);
    }

    public record ScriptCancellationTimedOutEvent(
        ScriptTicket ScriptTicket,
        string TaskId,
        ScriptIsolationLevel IsolationLevel,
        string MutexName,
        TimeSpan ScriptCancellationTimeoutBeforeAbandoning);
}