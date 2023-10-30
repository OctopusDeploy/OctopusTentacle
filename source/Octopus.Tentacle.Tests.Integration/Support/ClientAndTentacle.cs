using System;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;
using Octopus.TestPortForwarder;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ClientAndTentacle: IAsyncDisposable
    {
        readonly IHalibutRuntime halibutRuntime;
        readonly ILogger logger;

        public ServiceEndPoint ServiceEndPoint { get; }
        public Server Server { get; }
        public PortForwarder? PortForwarder { get; }
        public RunningTentacle RunningTentacle { get; }
        public TentacleClient TentacleClient { get; }
        public TemporaryDirectory TemporaryDirectory { get; }
        public RpcRetrySettings RpcRetrySettings { get; }

        public LegacyTentacleClientBuilder LegacyTentacleClientBuilder()
        {
            return new LegacyTentacleClientBuilder(halibutRuntime, ServiceEndPoint);
        }

        public ClientAndTentacle(IHalibutRuntime halibutRuntime,
            ServiceEndPoint serviceEndPoint,
            Server server,
            PortForwarder? portForwarder,
            RunningTentacle runningTentacle,
            TentacleClient tentacleClient,
            TemporaryDirectory temporaryDirectory, 
            RpcRetrySettings rpcRetrySettings,
            ILogger logger)
        {
            this.halibutRuntime = halibutRuntime;
            Server = server;
            PortForwarder = portForwarder;
            RunningTentacle = runningTentacle;
            TentacleClient = tentacleClient;
            TemporaryDirectory = temporaryDirectory;
            RpcRetrySettings = rpcRetrySettings;
            this.ServiceEndPoint = serviceEndPoint;
            this.logger = logger.ForContext<ClientAndTentacle>();
        }

        public async ValueTask DisposeAsync()
        {
            logger.Information("Starting DisposeAsync");

            logger.Information("Starting RunningTentacle.DisposeAsync and Server.Dispose and PortForwarder.Dispose");
            var portForwarderTask = Task.Run(() => PortForwarder?.Dispose());
            var runningTentacleTask = RunningTentacle.DisposeAsync();
            var serverTask = Server.DisposeAsync();
            await Task.WhenAll(runningTentacleTask.AsTask(), serverTask.AsTask(), portForwarderTask);

            logger.Information("Starting TentacleClient.Dispose");
            TentacleClient.Dispose();
            logger.Information("Starting TemporaryDirectory.Dispose");
            TemporaryDirectory.Dispose();
            logger.Information("Finished DisposeAsync");
        }
    }
}