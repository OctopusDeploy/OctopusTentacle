using System;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.ServiceHelpers;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Client.Scripts
{
    // TODO: this is not an orchestrator factory.
    class ScriptOrchestratorFactory
    {
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ITentacleClientTaskLog logger;

        readonly ClientsHolder clientsHolder;
        readonly TentacleClientOptions clientOptions;

        public ScriptOrchestratorFactory(
            ClientsHolder clientsHolder,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ITentacleClientTaskLog logger)
        {
            this.clientsHolder = clientsHolder;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }

        public IScriptExecutor CreateScriptExecutor(ScriptServiceVersion scriptServiceToUse)
        {
            if (scriptServiceToUse == ScriptServiceVersion.ScriptServiceVersion1)
            {
                return new ScriptServiceV1Executor(
                    clientsHolder.ScriptServiceV1,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    logger);
            }

            if (scriptServiceToUse == ScriptServiceVersion.ScriptServiceVersion2)
            {
                return new ScriptServiceV2Executor(
                    clientsHolder.ScriptServiceV2,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);
            }

            if (scriptServiceToUse == ScriptServiceVersion.KubernetesScriptServiceVersion1Alpha)
            {
                return new KubernetesScriptServiceV1AlphaExecutor(
                    clientsHolder.KubernetesScriptServiceV1Alpha,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);
            }
            
            if (scriptServiceToUse == ScriptServiceVersion.KubernetesScriptServiceVersion1)
            {
                return new KubernetesScriptServiceV1Executor(
                    clientsHolder.KubernetesScriptServiceV1,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);
            }

            throw new InvalidOperationException($"Unknown ScriptServiceVersion {scriptServiceToUse}");
        }

        
    }
}