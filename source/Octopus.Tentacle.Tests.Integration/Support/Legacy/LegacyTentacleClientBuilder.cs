using System.Threading;
using Halibut;
using Halibut.Util;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class LegacyTentacleClientBuilder
    {
        readonly IHalibutRuntime halibutRuntime;
        readonly ServiceEndPoint serviceEndPoint;

        public LegacyTentacleClientBuilder(IHalibutRuntime halibutRuntime, ServiceEndPoint serviceEndPoint)
        {
            this.halibutRuntime = halibutRuntime;
            this.serviceEndPoint = serviceEndPoint;
        }

        public LegacyTentacleClient Build()
        {
            var asyncScriptService = halibutRuntime.CreateAsyncClient<IScriptService, IAsyncClientScriptService>(serviceEndPoint);
            var asyncFileTransferService = halibutRuntime.CreateAsyncClient<IFileTransferService, IAsyncClientFileTransferService>(serviceEndPoint);
            var asyncCapabilitiesServiceV2 = halibutRuntime.CreateAsyncClient<ICapabilitiesServiceV2, IAsyncClientCapabilitiesServiceV2>(serviceEndPoint).WithBackwardsCompatability();

            return new LegacyTentacleClient(
                asyncScriptService,
                asyncFileTransferService,
                asyncCapabilitiesServiceV2);
        }
    }
}