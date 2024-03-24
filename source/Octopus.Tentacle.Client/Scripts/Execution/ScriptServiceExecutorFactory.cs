using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Scripts.Execution
{
    class ScriptServiceExecutorFactory
    {
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ILog logger;

        readonly IAsyncClientScriptService clientScriptServiceV1;
        readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha;
        readonly TentacleClientOptions clientOptions;

        public ScriptServiceExecutorFactory(
            IAsyncClientScriptService clientScriptServiceV1,
            IAsyncClientScriptServiceV2 clientScriptServiceV2,
            IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ILog logger)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.clientScriptServiceV3Alpha = clientScriptServiceV3Alpha;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }

        public IScriptServiceExecutor CreateExecutor(ScriptServiceVersion scriptServiceToUse)
        {
            return scriptServiceToUse switch
            {
                ScriptServiceVersion.Version1 =>
                    new ScriptServiceV1Executor(
                        clientScriptServiceV1,
                        rpcCallExecutor,
                        clientOperationMetricsBuilder,
                        logger),

                ScriptServiceVersion.Version2 =>
                    new ScriptServiceV2Executor(
                        clientScriptServiceV2,
                        rpcCallExecutor,
                        clientOperationMetricsBuilder,
                        onCancellationAbandonCompleteScriptAfter,
                        clientOptions,
                        logger),

                ScriptServiceVersion.Version3Alpha =>
                    new ScriptServiceV3AlphaExecutor(
                        clientScriptServiceV3Alpha,
                        rpcCallExecutor,
                        clientOperationMetricsBuilder,
                        onCancellationAbandonCompleteScriptAfter,
                        clientOptions,
                        logger),

                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}