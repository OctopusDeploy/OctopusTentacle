using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Serilog;
using Octopus.Tentacle.Client.Retries;

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
            using var disposeCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var cancellationToken = disposeCancellationTokenSource.Token;

            logger.Information("Starting ServerHalibutRuntime.DisposeAsync");
            var disposeTask = ServerHalibutRuntime.DisposeAsync().AsTask();
            var completed = await disposeTask.WaitTillCompletedOrCancelled(cancellationToken);
            if (!completed)
            {
                logger.Information("Could not Dispose of the ServerHalibutRuntime within 10 seconds");
            }

            logger.Information("Finished ServerHalibutRuntime.DisposeAsync");
        }
    }
}