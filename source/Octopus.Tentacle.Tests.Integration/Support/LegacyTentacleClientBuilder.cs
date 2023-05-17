using System;
using System.Threading;
using Halibut;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    internal class LegacyTentacleClientBuilder
    {
        IHalibutRuntime? halibutRuntime;
        Uri? serviceUri;
        string? remoteThumbprint;

        public LegacyTentacleClientBuilder()
        {
        }

        public LegacyTentacleClientBuilder(IHalibutRuntime halibutRuntime)
        {
            this.halibutRuntime = halibutRuntime;
        }

        public LegacyTentacleClientBuilder WithServiceUri(Uri serviceUri)
        {
            this.serviceUri = serviceUri;

            return this;
        }

        public LegacyTentacleClientBuilder WithRemoteThumbprint(string remoteThumbprint)
        {
            this.remoteThumbprint = remoteThumbprint;

            return this;
        }

        public LegacyTentacleClient Build(CancellationToken cancellationToken)
        {
            var serviceEndPoint = new ServiceEndPoint(this.serviceUri, this.remoteThumbprint);

            if (halibutRuntime == null)
            {
                halibutRuntime = new HalibutRuntimeBuilder()
                    .Build();
            }

            var scriptService = halibutRuntime.CreateClient<IScriptService>(serviceEndPoint, cancellationToken);
            var fileTransferService = halibutRuntime.CreateClient<IFileTransferService>(serviceEndPoint, cancellationToken);
            var capabilitiesServiceV2 = halibutRuntime.CreateClient<ICapabilitiesServiceV2>(serviceEndPoint, cancellationToken).WithBackwardsCompatability();

            return new LegacyTentacleClient(scriptService, fileTransferService, capabilitiesServiceV2);
        }
    }
}