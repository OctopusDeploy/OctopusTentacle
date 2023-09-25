using System;
using System.Threading.Tasks;
using Halibut;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class Server : IAsyncDisposable
    {
        private readonly ILogger logger;
        public IHalibutRuntime ServerHalibutRuntime { get; }
        public int ServerListeningPort { get; }

        public Server(IHalibutRuntime serverHalibutRuntime, int serverListeningPort, ILogger logger)
        {
            this.logger = logger.ForContext<Server>();
            this.ServerHalibutRuntime = serverHalibutRuntime;
            ServerListeningPort = serverListeningPort;
        }

        public async ValueTask DisposeAsync()
        {
            logger.Information("Starting ServerHalibutRuntime.DisposeAsync");
            await ServerHalibutRuntime.DisposeAsync();
            logger.Information("Finished ServerHalibutRuntime.DisposeAsync");
        }
    }
}