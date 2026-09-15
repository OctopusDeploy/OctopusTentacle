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
        /// When a script is unable to be cancelled for the scriptCancellationTimeoutBeforeAbandoning period,
        /// the script will be abandoned and this method will be called.
        /// The specifics of the script abandoned during cancellation are provided.
        /// </summary>
        void ScriptCancellationTimedOut(ScriptCancellationTimedOutEvent scriptCancellationTimedOutEvent);
    }
}