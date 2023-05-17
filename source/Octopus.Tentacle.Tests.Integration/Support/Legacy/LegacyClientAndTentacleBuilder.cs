using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    internal class LegacyClientAndTentacleBuilder
    {
        private readonly TentacleType tentacleType;
        private string? tentacleVersion;

        public LegacyClientAndTentacleBuilder(TentacleType tentacleType)
        {
            this.tentacleType = tentacleType;
        }

        public LegacyClientAndTentacleBuilder WithTentacleVersion(string tentacleVersion)
        {
            this.tentacleVersion = tentacleVersion;
            return this;
        }

        public async Task<LegacyClientAndTentacle> Build(CancellationToken cancellationToken)
        {
            // Server
            var serverHalibutRuntime = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Server)
                .WithLegacyContractSupport()
                .Build();

            serverHalibutRuntime.Trust(Certificates.TentaclePublicThumbprint);
            var serverListeningPort = serverHalibutRuntime.Listen();

            var server = new Server(serverHalibutRuntime, serverListeningPort);

            // Port Forwarder
            var portForwarder = PortForwarderBuilder.ForwardingToLocalPort(serverListeningPort).Build();
            RunningTentacle runningTentacle;

            // Tentacle
            var temporaryDirectory = new TemporaryDirectory();
            var tentacleExe = string.IsNullOrWhiteSpace(tentacleVersion) ?
                TentacleExeFinder.FindTentacleExe() :
                await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion);

            if (tentacleType == TentacleType.Polling)
            {
                runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(cancellationToken);
            }
            else
            {
                runningTentacle = await new ListeningTentacleBuilder(Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(cancellationToken);
            }

            var tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

            var tentacleClient = new LegacyTentacleClientBuilder(server.ServerHalibutRuntime, tentacleEndPoint)
                .Build(cancellationToken);

            return new LegacyClientAndTentacle(server, portForwarder, runningTentacle, tentacleClient, temporaryDirectory);
        }
    }
}