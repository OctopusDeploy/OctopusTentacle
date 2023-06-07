using System;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    internal class LegacyClientAndTentacle : IDisposable
    {
        private readonly TemporaryDirectory temporaryDirectory;
        public Server Server { get; }
        public PortForwarder PortForwarder { get; }
        public RunningTentacle RunningTentacle { get; }
        public LegacyTentacleClient TentacleClient { get; }

        public LegacyClientAndTentacle(
            Server server,
            PortForwarder portForwarder,
            RunningTentacle runningTentacle,
            LegacyTentacleClient tentacleClient,
            TemporaryDirectory temporaryDirectory)
        {
            this.temporaryDirectory = temporaryDirectory;
            Server = server;
            PortForwarder = portForwarder;
            RunningTentacle = runningTentacle;
            TentacleClient = tentacleClient;
        }

        public void Dispose()
        {
            Server.Dispose();
            PortForwarder.Dispose();
            RunningTentacle.Dispose();
            temporaryDirectory.Dispose();
        }
    }
}