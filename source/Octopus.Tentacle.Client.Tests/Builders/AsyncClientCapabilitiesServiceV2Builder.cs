using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    public class AsyncClientCapabilitiesServiceV2Builder
    {
        private readonly List<string> capabilities = new();

        public AsyncClientCapabilitiesServiceV2Builder WithCapability(string capability)
        {
            capabilities.Add(capability);
            return this;
        }

        public AsyncClientCapabilitiesServiceV2Builder WithCapabilities(params string[] capabilities)
        {
            this.capabilities.AddRange(capabilities);
            return this;
        }

        public AsyncClientCapabilitiesServiceV2Builder RemoveCapability(string capability)
        {
            capabilities.Remove(capability);
            return this;
        }

        public AsyncClientCapabilitiesServiceV2Builder ClearCapabilities()
        {
            capabilities.Clear();
            return this;
        }

        public IAsyncClientCapabilitiesServiceV2 Build() =>
            new FixedCapabilitiesService(capabilities);

        public static IAsyncClientCapabilitiesServiceV2 Default() => new AsyncClientCapabilitiesServiceV2Builder()
            .WithCapability(nameof(IScriptServiceV2))
            .WithCapability(nameof(IScriptServiceV3Alpha))
            .Build();

        class FixedCapabilitiesService : IAsyncClientCapabilitiesServiceV2
        {
            readonly List<string> capabilities;

            public FixedCapabilitiesService(List<string> capabilities)
            {
                this.capabilities = capabilities;
            }

            public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions halibutProxyRequestOptions)
            {
                await Task.CompletedTask;
                return new CapabilitiesResponseV2(capabilities.ToList());
            }
        }
    }
}