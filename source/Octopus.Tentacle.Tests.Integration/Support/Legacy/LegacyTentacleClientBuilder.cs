using System.Threading;
using Halibut;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;

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

        public LegacyTentacleClient Build(CancellationToken cancellationToken)
        {
            var scriptService = halibutRuntime.CreateClient<IScriptService>(serviceEndPoint, cancellationToken);
            var fileTransferService = halibutRuntime.CreateClient<IFileTransferService>(serviceEndPoint, cancellationToken);
            var capabilitiesServiceV2 = halibutRuntime.CreateClient<ICapabilitiesServiceV2>(serviceEndPoint, cancellationToken).WithBackwardsCompatability();

            return new LegacyTentacleClient(scriptService, fileTransferService, capabilitiesServiceV2);
        }
    }
}