using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Util;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Services;
using Octopus.Tentacle.Client.Utils;
using Octopus.Tentacle.Contracts.Capabilities;
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
        readonly RpcRetrySettings rpcRetrySettings;
        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly ILog logger;

        readonly SyncAndAsyncClientScriptServiceV1 clientScriptServiceV1;
        readonly SyncAndAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly SyncAndAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2;

        public ScriptOrchestratorFactory(
            SyncAndAsyncClientScriptServiceV1 clientScriptServiceV1,
            SyncAndAsyncClientScriptServiceV2 clientScriptServiceV2,
            SyncAndAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            RpcRetrySettings rpcRetrySettings,
            AsyncHalibutFeature asyncHalibutFeature,
            ILog logger)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.clientCapabilitiesServiceV2 = clientCapabilitiesServiceV2;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.rpcRetrySettings = rpcRetrySettings;
            this.asyncHalibutFeature = asyncHalibutFeature;
            this.logger = logger;
        }

        public async Task<IScriptOrchestrator> CreateOrchestrator(CancellationToken cancellationToken)
        {
            var scriptServiceToUse = await DetermineScriptServiceVersionToUse(cancellationToken);

            return scriptServiceToUse switch
            {
                ScriptServiceVersion.Version1 => new ScriptServiceV1Orchestrator(
                    clientScriptServiceV1,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    asyncHalibutFeature,
                    logger),

                ScriptServiceVersion.Version2 => new ScriptServiceV2Orchestrator(
                    clientScriptServiceV2,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    clientOperationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    onCancellationAbandonCompleteScriptAfter,
                    rpcRetrySettings,
                    asyncHalibutFeature,
                    logger),

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(CancellationToken cancellationToken)
        {
            logger.Verbose("Determining ScriptService version to use");

            CapabilitiesResponseV2 tentacleCapabilities;

            async Task<CapabilitiesResponseV2> GetCapabilitiesFunc(CancellationToken ct)
            {
                var result = await asyncHalibutFeature
                    .WhenDisabled(() => clientCapabilitiesServiceV2.SyncService.GetCapabilities(new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                    .WhenEnabled(async () => await clientCapabilitiesServiceV2.AsyncService.GetCapabilitiesAsync(new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                return result;
            }

            if (rpcRetrySettings.RetriesEnabled)
            {
                tentacleCapabilities = await rpcCallExecutor.ExecuteWithRetries(
                    RpcCall.Create<ICapabilitiesServiceV2>(nameof(ICapabilitiesServiceV2.GetCapabilities)),
                    GetCapabilitiesFunc,
                    logger,
                    // We can abandon a call to Get Capabilities and walk away as this is not running anything that needs to be cancelled on Tentacle
                    abandonActionOnCancellation: true,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                tentacleCapabilities = await rpcCallExecutor.ExecuteWithNoRetries(
                    RpcCall.Create<ICapabilitiesServiceV2>(nameof(ICapabilitiesServiceV2.GetCapabilities)),
                    GetCapabilitiesFunc,
                    logger,
                    abandonActionOnCancellation: true,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
            }

            logger.Verbose($"Discovered Tentacle capabilities: {string.Join(",", tentacleCapabilities.SupportedCapabilities)}");

            if (tentacleCapabilities.HasScriptServiceV2())
            {
                logger.Verbose("Using ScriptServiceV2");
                logger.Verbose(rpcRetrySettings.RetriesEnabled
                    ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                    : "RPC call retries are disabled.");
                return ScriptServiceVersion.Version2;
            }

            logger.Verbose("RPC call retries are enabled but will not be used for Script Execution as a compatible ScriptService was not found. Please upgrade Tentacle to enable this feature.");
            logger.Verbose("Using ScriptServiceV1");
            return ScriptServiceVersion.Version1;
        }
    }
}