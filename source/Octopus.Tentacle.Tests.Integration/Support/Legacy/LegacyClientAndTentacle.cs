using System;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    internal class LegacyClientAndTentacle : IAsyncDisposable
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

        public async ValueTask DisposeAsync()
        {
            Server.Dispose();
            PortForwarder.Dispose();
            await RunningTentacle.DisposeAsync();
            temporaryDirectory.Dispose();
        }
    }
}