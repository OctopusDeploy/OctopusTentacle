using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Kubernetes;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;

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
        readonly ISomethingLog logger;

        readonly IAsyncClientScriptService clientScriptServiceV1;
        readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly IAsyncClientKubernetesScriptServiceV1Alpha clientKubernetesScriptServiceV1Alpha;
        readonly IAsyncClientKubernetesScriptServiceV1 clientKubernetesScriptServiceV1;
        readonly IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2;
        readonly TentacleClientOptions clientOptions;

        public ScriptOrchestratorFactory(
            IAsyncClientScriptService clientScriptServiceV1,
            IAsyncClientScriptServiceV2 clientScriptServiceV2,
            IAsyncClientKubernetesScriptServiceV1Alpha clientKubernetesScriptServiceV1Alpha,
            IAsyncClientKubernetesScriptServiceV1 clientKubernetesScriptServiceV1,
            IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ISomethingLog logger)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.clientKubernetesScriptServiceV1Alpha = clientKubernetesScriptServiceV1Alpha;
            this.clientKubernetesScriptServiceV1 = clientKubernetesScriptServiceV1;
            this.clientCapabilitiesServiceV2 = clientCapabilitiesServiceV2;
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
                scriptServiceToUse = await DetermineScriptServiceVersionToUse(cancellationToken);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Script execution was cancelled", ex);
            }

            if (scriptServiceToUse == ScriptServiceVersion.ScriptServiceVersion1)
            {
                return new ScriptServiceV1Orchestrator(
                    clientScriptServiceV1,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    clientOptions,
                    logger);
            }

            if (scriptServiceToUse == ScriptServiceVersion.ScriptServiceVersion2)
            {
                return new ScriptServiceV2Orchestrator(
                    clientScriptServiceV2,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);
            }

            if (scriptServiceToUse == ScriptServiceVersion.KubernetesScriptServiceVersion1Alpha)
            {
                return new KubernetesScriptServiceV1AlphaOrchestrator(
                    clientKubernetesScriptServiceV1Alpha,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);
            }
            
            if (scriptServiceToUse == ScriptServiceVersion.KubernetesScriptServiceVersion1)
            {
                return new KubernetesScriptServiceV1Orchestrator(
                    clientKubernetesScriptServiceV1,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    onCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);
            }

            throw new InvalidOperationException($"Unknown ScriptServiceVersion {scriptServiceToUse}");
        }

        async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(CancellationToken cancellationToken)
        {
            logger.Verbose("Determining ScriptService version to use");

            async Task<CapabilitiesResponseV2> GetCapabilitiesFunc(CancellationToken ct)
            {
                var result = await clientCapabilitiesServiceV2.GetCapabilitiesAsync(new HalibutProxyRequestOptions(ct));

                return result;
            }

            var tentacleCapabilities = await rpcCallExecutor.Execute(
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<ICapabilitiesServiceV2>(nameof(ICapabilitiesServiceV2.GetCapabilities)),
                GetCapabilitiesFunc,
                logger,
                clientOperationMetricsBuilder,
                cancellationToken);

            logger.Verbose($"Discovered Tentacle capabilities: {string.Join(",", tentacleCapabilities.SupportedCapabilities)}");

            // Check if we support any kubernetes script service.
            // It's implied (and tested) that GetCapabilities will only return Kubernetes or non-Kubernetes script services, never a mix
            if (tentacleCapabilities.HasAnyKubernetesScriptService())
            {
                return DetermineKubernetesScriptServiceVersionToUse(tentacleCapabilities);
            }

            return DetermineShellScriptServiceVersionToUse(tentacleCapabilities);
        }

        ScriptServiceVersion DetermineShellScriptServiceVersionToUse(CapabilitiesResponseV2 tentacleCapabilities)
        {
            if (tentacleCapabilities.HasScriptServiceV2())
            {
                logger.Verbose("Using ScriptServiceV2");
                logger.Verbose(clientOptions.RpcRetrySettings.RetriesEnabled
                    ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                    : "RPC call retries are disabled.");
                return ScriptServiceVersion.ScriptServiceVersion2;
            }

            logger.Verbose("RPC call retries are enabled but will not be used for Script Execution as a compatible ScriptService was not found. Please upgrade Tentacle to enable this feature.");
            logger.Verbose("Using ScriptServiceV1");
            return ScriptServiceVersion.ScriptServiceVersion1;
        }

        ScriptServiceVersion DetermineKubernetesScriptServiceVersionToUse(CapabilitiesResponseV2 tentacleCapabilities)
        {
            if (tentacleCapabilities.HasKubernetesScriptServiceV1())
            {
                logger.Verbose($"Using KubernetesScriptServiceV1");
                logger.Verbose(clientOptions.RpcRetrySettings.RetriesEnabled
                    ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                    : "RPC call retries are disabled.");
                
                return ScriptServiceVersion.KubernetesScriptServiceVersion1;
            }

            logger.Verbose($"Using KubernetesScriptServiceV1Alpha");
            logger.Verbose(clientOptions.RpcRetrySettings.RetriesEnabled
                ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                : "RPC call retries are disabled.");

            //this is the only supported kubernetes script service
            return ScriptServiceVersion.KubernetesScriptServiceVersion1Alpha;
        }
    }
}