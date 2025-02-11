using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Kubernetes;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Scripts
{
    public class ScriptServiceVersionSelector
    {
        readonly ITentacleClientTaskLog logger;
        readonly IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly TentacleClientOptions clientOptions;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;

        internal ScriptServiceVersionSelector(
            IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2, 
            ITentacleClientTaskLog logger, 
            RpcCallExecutor rpcCallExecutor,
            TentacleClientOptions clientOptions, 
            ClientOperationMetricsBuilder clientOperationMetricsBuilder)
        {
            this.clientCapabilitiesServiceV2 = clientCapabilitiesServiceV2;
            this.logger = logger;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOptions = clientOptions;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
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

            // Check if we support any kubernetes script service.
            // It's implied (and tested) that GetCapabilities will only return Kubernetes or non-Kubernetes script services, never a mix
            if (tentacleCapabilities.HasAnyKubernetesScriptService())
            {
                return DetermineKubernetesScriptServiceVersionToUse();
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

        ScriptServiceVersion DetermineKubernetesScriptServiceVersionToUse()
        {
            logger.Verbose($"Using KubernetesScriptServiceV1");
            logger.Verbose(clientOptions.RpcRetrySettings.RetriesEnabled
                ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                : "RPC call retries are disabled.");

            return ScriptServiceVersion.KubernetesScriptServiceVersion1;
        }
    }
}