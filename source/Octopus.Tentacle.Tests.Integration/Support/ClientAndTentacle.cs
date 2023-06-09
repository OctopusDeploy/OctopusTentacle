using System;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ClientAndTentacle: IDisposable
    {
        private readonly IHalibutRuntime halibutRuntime;
        private readonly ServiceEndPoint serviceEndPoint;
        public Server Server { get; }
        public PortForwarder? PortForwarder { get; }
        public RunningTentacle RunningTentacle { get; }
        public TentacleClient TentacleClient { get; }
        public TemporaryDirectory TemporaryDirectory { get; }

        public LegacyTentacleClientBuilder LegacyTentacleClientBuilder()
        {
            return new LegacyTentacleClientBuilder(halibutRuntime, serviceEndPoint);
        }

        public ClientAndTentacle(
            IHalibutRuntime halibutRuntime,
            ServiceEndPoint serviceEndPoint,
            Server server,
            PortForwarder? portForwarder,
            RunningTentacle runningTentacle,
            TentacleClient tentacleClient,
            TemporaryDirectory temporaryDirectory)
        {
            this.halibutRuntime = halibutRuntime;
            Server = server;
            PortForwarder = portForwarder;
            RunningTentacle = runningTentacle;
            TentacleClient = tentacleClient;
            TemporaryDirectory = temporaryDirectory;
            this.serviceEndPoint = serviceEndPoint;
        }

        public void Dispose()
        {
            Server.Dispose();
            PortForwarder?.Dispose();
            RunningTentacle.Dispose();
            TentacleClient.Dispose();
            TemporaryDirectory.Dispose();
        }
    }
}