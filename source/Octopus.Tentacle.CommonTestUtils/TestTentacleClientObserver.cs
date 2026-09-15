using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.CommonTestUtils
{
    public class TestTentacleClientObserver : ITentacleClientObserver
    {
        private readonly List<RpcCallMetrics> rpcCallMetrics = new();
        private readonly List<ClientOperationMetrics> uploadFileMetrics = new();
        private readonly List<ClientOperationMetrics> downloadFileMetrics = new();
        private readonly List<ClientOperationMetrics> executeScriptMetrics = new();
        private readonly List<ScriptCancellationTimedOutEvent> scriptCancellationTimedOutEvents = new();

        public IReadOnlyList<RpcCallMetrics> RpcCallMetrics => rpcCallMetrics;
        public IReadOnlyList<ClientOperationMetrics> UploadFileMetrics => uploadFileMetrics;
        public IReadOnlyList<ClientOperationMetrics> DownloadFileMetrics => downloadFileMetrics;
        public IReadOnlyList<ClientOperationMetrics> ExecuteScriptMetrics => executeScriptMetrics;
        public IReadOnlyList<ScriptCancellationTimedOutEvent> ScriptCancellationTimedOutEvents => scriptCancellationTimedOutEvents;

        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITentacleClientTaskLog logger)
        {
            this.rpcCallMetrics.Add(rpcCallMetrics);
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger)
        {
            uploadFileMetrics.Add(clientOperationMetrics);
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger)
        {
            downloadFileMetrics.Add(clientOperationMetrics);
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger)
        {
            executeScriptMetrics.Add(clientOperationMetrics);
        }

        public void ScriptCancellationTimedOut(
            ScriptTicket scriptTicket,
            string taskId,
            ScriptIsolationLevel isolationLevel,
            string mutexName,
            TimeSpan scriptCancellationTimeoutBeforeAbandoning)
        {
            scriptCancellationTimedOutEvents.Add(new ScriptCancellationTimedOutEvent(
                scriptTicket,
                taskId,
                isolationLevel,
                mutexName,
                scriptCancellationTimeoutBeforeAbandoning));
        }
    }

    public record ScriptCancellationTimedOutEvent(
        ScriptTicket ScriptTicket,
        string TaskId,
        ScriptIsolationLevel IsolationLevel,
        string MutexName,
        TimeSpan ScriptCancellationTimeoutBeforeAbandoning);
}