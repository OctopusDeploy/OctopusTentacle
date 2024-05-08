using System;
using System.Collections.Generic;
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

        public IReadOnlyList<RpcCallMetrics> RpcCallMetrics => rpcCallMetrics;
        public IReadOnlyList<ClientOperationMetrics> UploadFileMetrics => uploadFileMetrics;
        public IReadOnlyList<ClientOperationMetrics> DownloadFileMetrics => downloadFileMetrics;
        public IReadOnlyList<ClientOperationMetrics> ExecuteScriptMetrics => executeScriptMetrics;

        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITaskLog logger)
        {
            this.rpcCallMetrics.Add(rpcCallMetrics);
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
            uploadFileMetrics.Add(clientOperationMetrics);
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
            downloadFileMetrics.Add(clientOperationMetrics);
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
            executeScriptMetrics.Add(clientOperationMetrics);
        }
    }
}