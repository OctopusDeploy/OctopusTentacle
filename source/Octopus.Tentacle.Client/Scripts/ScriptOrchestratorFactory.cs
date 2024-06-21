using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.ServiceHelpers;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptOrchestratorFactory : IScriptOrchestratorFactory
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly OnScriptStatusResponseReceived onScriptStatusResponseReceived;
        readonly OnScriptCompleted onScriptCompleted;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ITentacleClientTaskLog logger;

        readonly ClientsHolder clientsHolder;
        
        readonly TentacleClientOptions clientOptions;

        public ScriptOrchestratorFactory(
            ClientsHolder clientsHolder,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ITentacleClientTaskLog logger)
        {
            this.clientsHolder = clientsHolder;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }

        public async Task<IScriptOrchestrator> CreateOrchestrator(CancellationToken cancellationToken)
        {
            ScriptServiceVersion scriptServiceToUse;
            try
            {
                var scriptServicePicker = new ScriptServicePicker(clientsHolder.CapabilitiesServiceV2, logger, rpcCallExecutor, clientOptions, clientOperationMetricsBuilder);
                scriptServiceToUse = await scriptServicePicker.DetermineScriptServiceVersionToUse(cancellationToken);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Script execution was cancelled", ex);
            }

            var structuredScriptOrchestrator = CreateOrchestrator(scriptServiceToUse);
            return new ObservingScriptOrchestrator(scriptObserverBackOffStrategy, onScriptStatusResponseReceived, onScriptCompleted, structuredScriptOrchestrator, logger);
        }

        public IStructuredScriptExecutor CreateOrchestrator(ScriptServiceVersion scriptServiceToUse)
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