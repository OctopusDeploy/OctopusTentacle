using System;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts.Execution;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptOrchestratorFactory
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly OnScriptStatusResponseReceived onScriptStatusResponseReceived;
        readonly OnScriptCompleted onScriptCompleted;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ILog logger;

        readonly IAsyncClientScriptService clientScriptServiceV1;
        readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha;
        readonly TentacleClientOptions clientOptions;

        public ScriptOrchestratorFactory(
            IAsyncClientScriptService clientScriptServiceV1,
            IAsyncClientScriptServiceV2 clientScriptServiceV2,
            IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ILog logger)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.clientScriptServiceV3Alpha = clientScriptServiceV3Alpha;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }

        public IScriptOrchestrator CreateOrchestrator(ScriptServiceVersion scriptServiceToUse)
        {
            var scriptServiceExecutorFactory = new ScriptServiceExecutorFactory(
                clientScriptServiceV1,
                clientScriptServiceV2,
                clientScriptServiceV3Alpha,
                rpcCallExecutor,
                clientOperationMetricsBuilder,
                onCancellationAbandonCompleteScriptAfter,
                clientOptions,
                logger);

            var scriptServiceExecutor = scriptServiceExecutorFactory.CreateExecutor(scriptServiceToUse);

            return scriptServiceToUse switch
            {
                ScriptServiceVersion.Version1 => new ScriptServiceV1Orchestrator(
                    scriptServiceExecutor,
                    scriptObserverBackOffStrategy,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    clientOptions),

                ScriptServiceVersion.Version2 => new ScriptServiceV2Orchestrator(
                    scriptServiceExecutor,
                    scriptObserverBackOffStrategy,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    clientOptions),

                ScriptServiceVersion.Version3Alpha => new ScriptServiceV3AlphaOrchestrator(
                    scriptServiceExecutor,
                    scriptObserverBackOffStrategy,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    clientOptions),

                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}