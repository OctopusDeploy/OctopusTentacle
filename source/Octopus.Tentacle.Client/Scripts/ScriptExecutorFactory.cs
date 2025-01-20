using System;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.ServiceHelpers;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptExecutorFactory
    {
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ITentacleClientTaskLog logger;

        readonly AllClients allClients;
        readonly TentacleClientOptions clientOptions;

        public ScriptExecutorFactory(
            AllClients allClients,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ITentacleClientTaskLog logger)
        {
            this.allClients = allClients;
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
                    allClients.ScriptServiceV1,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    logger);
            }

            if (scriptServiceToUse == ScriptServiceVersion.ScriptServiceVersion2)
            {
                return new ScriptServiceV2Executor(
                    allClients.ScriptServiceV2,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);
            }

            if (scriptServiceToUse == ScriptServiceVersion.KubernetesScriptServiceVersion1)
            {
                return new KubernetesScriptServiceV1Executor(
                    allClients.KubernetesScriptServiceV1,
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