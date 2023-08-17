using System;
using System.Collections.Generic;
using Octopus.Diagnostics;
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

        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ILog logger)
        {
            this.rpcCallMetrics.Add(rpcCallMetrics);
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
        {
            uploadFileMetrics.Add(clientOperationMetrics);
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
        {
            downloadFileMetrics.Add(clientOperationMetrics);
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
        {
            executeScriptMetrics.Add(clientOperationMetrics);
        }
    }
}