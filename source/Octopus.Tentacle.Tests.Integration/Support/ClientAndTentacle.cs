using System;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ClientAndTentacle: IDisposable
    {
        public Server Server { get; }
        public PortForwarder PortForwarder { get; }
        public RunningTentacle RunningTentacle { get; }
        public TentacleClient TentacleClient { get; }
        public TemporaryDirectory TemporaryDirectory { get; }

        public ClientAndTentacle(
            Server server,
            PortForwarder portForwarder,
            RunningTentacle runningTentacle,
            TentacleClient tentacleClient,
            TemporaryDirectory temporaryDirectory)
        {
            Server = server;
            PortForwarder = portForwarder;
            RunningTentacle = runningTentacle;
            TentacleClient = tentacleClient;
            TemporaryDirectory = temporaryDirectory;
        }

        public void Dispose()
        {
            Server.Dispose();
            PortForwarder.Dispose();
            RunningTentacle.Dispose();
            TentacleClient.Dispose();
            TemporaryDirectory.Dispose();
        }
    }
}