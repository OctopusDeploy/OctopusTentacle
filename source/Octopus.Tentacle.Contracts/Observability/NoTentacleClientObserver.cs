using System;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoTentacleClientObserver : ITentacleClientObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITentacleClientTaskLog logger)
        {
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger)
        {
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger)
        {
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger)
        {
        }

        public void ScriptCancellationTimedOut(
            ScriptTicket scriptTicket,
            string taskId,
            ScriptIsolationLevel isolationLevel,
            string mutexName,
            TimeSpan scriptCancellationTimeoutBeforeAbandoning)
        {
        }
    }
}