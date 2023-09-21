using System;
using System.Threading.Tasks;
using Halibut;
using Halibut.Util;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ClientAndTentacle: IAsyncDisposable
    {
        private readonly IHalibutRuntime halibutRuntime;
        public ServiceEndPoint ServiceEndPoint { get; }
        public Server Server { get; }
        public PortForwarder? PortForwarder { get; }
        public RunningTentacle RunningTentacle { get; }
        public TentacleClient TentacleClient { get; }
        public TemporaryDirectory TemporaryDirectory { get; }
        public RpcRetrySettings RpcRetrySettings { get; }

        public LegacyTentacleClientBuilder LegacyTentacleClientBuilder(AsyncHalibutFeature asyncHalibutFeature)
        {
            return new LegacyTentacleClientBuilder(halibutRuntime, ServiceEndPoint, asyncHalibutFeature);
        }

        public ClientAndTentacle(IHalibutRuntime halibutRuntime,
            ServiceEndPoint serviceEndPoint,
            Server server,
            PortForwarder? portForwarder,
            RunningTentacle runningTentacle,
            TentacleClient tentacleClient,
            TemporaryDirectory temporaryDirectory, 
            RpcRetrySettings rpcRetrySettings)
        {
            this.halibutRuntime = halibutRuntime;
            Server = server;
            PortForwarder = portForwarder;
            RunningTentacle = runningTentacle;
            TentacleClient = tentacleClient;
            TemporaryDirectory = temporaryDirectory;
            RpcRetrySettings = rpcRetrySettings;
            this.ServiceEndPoint = serviceEndPoint;
        }

        public async ValueTask DisposeAsync()
        {
            Server.Dispose();
            PortForwarder?.Dispose();
            await RunningTentacle.DisposeAsync();
            TentacleClient.Dispose();
            TemporaryDirectory.Dispose();
        }
    }
}