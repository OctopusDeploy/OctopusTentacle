using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Scripts.Execution
{
    class ScriptServiceVersionFactory
    {
        readonly IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TentacleClientOptions clientOptions;
        readonly ILog logger;

        public ScriptServiceVersionFactory(
            IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            TentacleClientOptions clientOptions,
            ILog logger)
        {
            this.clientCapabilitiesServiceV2 = clientCapabilitiesServiceV2;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }

        public async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(CancellationToken cancellationToken)
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

            if (tentacleCapabilities.HasScriptServiceV3Alpha())
            {
                //if the service is not disabled, we can use it :)
                if (!clientOptions.DisableScriptServiceV3Alpha)
                {
                    logger.Verbose("Using ScriptServiceV3Alpha");
                    logger.Verbose(clientOptions.RpcRetrySettings.RetriesEnabled
                        ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                        : "RPC call retries are disabled.");
                    return ScriptServiceVersion.Version3Alpha;
                }

                logger.Verbose("ScriptServiceV3Alpha is disabled and will not be used.");
            }

            if (tentacleCapabilities.HasScriptServiceV2())
            {
                logger.Verbose("Using ScriptServiceV2");
                logger.Verbose(clientOptions.RpcRetrySettings.RetriesEnabled
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