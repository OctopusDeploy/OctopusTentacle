using System.Threading;
using Halibut;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class TentacleClientBuilder
    {
        readonly IHalibutRuntime halibutRuntime;
        readonly ServiceEndPoint serviceEndPoint;

        public TentacleClientBuilder(IHalibutRuntime halibutRuntime, ServiceEndPoint serviceEndPoint)
        {
            this.halibutRuntime = halibutRuntime;
            this.serviceEndPoint = serviceEndPoint;
        }

        public TentacleClient Build(CancellationToken cancellationToken)
        {
            var scriptService = halibutRuntime.CreateClient<IScriptService>(serviceEndPoint, cancellationToken);
            var fileTransferService = halibutRuntime.CreateClient<IFileTransferService>(serviceEndPoint, cancellationToken);
            var capabilitiesServiceV2 = halibutRuntime.CreateClient<ICapabilitiesServiceV2>(serviceEndPoint, cancellationToken).WithBackwardsCompatability();

            return new TentacleClient(scriptService, fileTransferService, capabilitiesServiceV2);
        }
    }
}