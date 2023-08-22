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
        private readonly AsyncHalibutFeature asyncHalibutFeature;

        public LegacyTentacleClientBuilder(IHalibutRuntime halibutRuntime, ServiceEndPoint serviceEndPoint, AsyncHalibutFeature asyncHalibutFeature)
        {
            this.halibutRuntime = halibutRuntime;
            this.serviceEndPoint = serviceEndPoint;
            this.asyncHalibutFeature = asyncHalibutFeature;
        }

        public LegacyTentacleClient Build(CancellationToken cancellationToken)
        {
            if (asyncHalibutFeature.IsDisabled())
            {
#pragma warning disable CS0612
                var syncScriptService = halibutRuntime.CreateClient<IScriptService>(serviceEndPoint, cancellationToken);
                var syncFileTransferService = halibutRuntime.CreateClient<IFileTransferService>(serviceEndPoint, cancellationToken);
                var syncCapabilitiesServiceV2 = halibutRuntime.CreateClient<ICapabilitiesServiceV2>(serviceEndPoint, cancellationToken).WithBackwardsCompatability();
#pragma warning restore CS0612

                return new LegacyTentacleClient(
                    new(syncScriptService, null),
                    new(syncFileTransferService, null),
                    new(syncCapabilitiesServiceV2, null));
            }
            else
            {
                var asyncScriptService = halibutRuntime.CreateAsyncClient<IScriptService, IAsyncClientScriptService>(serviceEndPoint);
                var asyncFileTransferService = halibutRuntime.CreateAsyncClient<IFileTransferService, IAsyncClientFileTransferService>(serviceEndPoint);
                var asyncCapabilitiesServiceV2 = halibutRuntime.CreateAsyncClient<ICapabilitiesServiceV2, IAsyncClientCapabilitiesServiceV2>(serviceEndPoint).WithBackwardsCompatability();

                return new LegacyTentacleClient(
                    new(null, asyncScriptService),
                    new(null, asyncFileTransferService),
                    new(null, asyncCapabilitiesServiceV2));
            }
        }
    }
}