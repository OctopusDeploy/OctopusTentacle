using System;
using System.Threading;
using Halibut;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client
{
    public class TentacleClientBuilder
    {
        IHalibutRuntime? halibutRuntime;
        Uri? serviceUri;
        string? remoteThumbprint;

        public TentacleClientBuilder()
        {
        }

        public TentacleClientBuilder(IHalibutRuntime halibutRuntime)
        {
            this.halibutRuntime = halibutRuntime;
        }

        public TentacleClientBuilder WithServiceUri(Uri serviceUri)
        {
            this.serviceUri = serviceUri;

            return this;
        }

        public TentacleClientBuilder WithRemoteThumbprint(string remoteThumbprint)
        {
            this.remoteThumbprint = remoteThumbprint;

            return this;
        }

        public TentacleClient Build(CancellationToken cancellationToken)
        {
            var serviceEndPoint = new ServiceEndPoint(this.serviceUri, this.remoteThumbprint);

            if (halibutRuntime == null)
            {
                halibutRuntime = new HalibutRuntimeBuilder()
                    .Build();
            }

            var scriptService = halibutRuntime.CreateClient<IScriptService>(serviceEndPoint, cancellationToken);
            var fileTransferService = halibutRuntime.CreateClient<IFileTransferService>(serviceEndPoint, cancellationToken);

            return new TentacleClient(scriptService, fileTransferService);
        }
    }
}
