using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.TestPortForwarder;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    internal class LegacyClientAndTentacle : IAsyncDisposable
    {
        private readonly TemporaryDirectory temporaryDirectory;
        private readonly ILogger logger;
        public Server Server { get; }
        public PortForwarder PortForwarder { get; }
        public RunningTentacle RunningTentacle { get; }
        public LegacyTentacleClient TentacleClient { get; }

        public LegacyClientAndTentacle(Server server,
            PortForwarder portForwarder,
            RunningTentacle runningTentacle,
            LegacyTentacleClient tentacleClient,
            TemporaryDirectory temporaryDirectory, 
            ILogger logger)
        {
            this.temporaryDirectory = temporaryDirectory;
            this.logger = logger;
            Server = server;
            PortForwarder = portForwarder;
            RunningTentacle = runningTentacle;
            TentacleClient = tentacleClient;
        }

        public async ValueTask DisposeAsync()
        {
            logger.Information("Starting DisposeAsync");
            logger.Information("Starting Server.Dispose");
            Server.Dispose();
            logger.Information("Starting PortForwarder.Dispose");
            PortForwarder.Dispose();
            logger.Information("Starting RunningTentacle.DisposeAsync");
            await RunningTentacle.DisposeAsync();
            logger.Information("temporaryDirectory.Dispose");
            temporaryDirectory.Dispose();
            logger.Information("Finished DisposeAsync");
        }
    }
}