using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    internal class ClientAndTentacleBuilder
    {
        ITentacleServiceDecorator? tentacleServiceDecorator;
        TimeSpan retryDuration = TimeSpan.FromMinutes(2);
        IScriptObserverBackoffStrategy scriptObserverBackoffStrategy = new DefaultScriptObserverBackoffStrategy();
        readonly TentacleType tentacleType;
        string? tentacleVersion;

        public ClientAndTentacleBuilder(TentacleType tentacleType)
        {
            this.tentacleType = tentacleType;
        }

        public ClientAndTentacleBuilder WithTentacleServiceDecorator(ITentacleServiceDecorator tentacleServiceDecorator)
        {
            this.tentacleServiceDecorator = tentacleServiceDecorator;

            return this;
        }

        public ClientAndTentacleBuilder WithRetryDuration(TimeSpan retryDuration)
        {
            this.retryDuration = retryDuration;

            return this;
        }

        public ClientAndTentacleBuilder WithScriptObserverBackoffStrategy(IScriptObserverBackoffStrategy scriptObserverBackoffStrategy)
        {
            this.scriptObserverBackoffStrategy = scriptObserverBackoffStrategy;

            return this;
        }

        public ClientAndTentacleBuilder WithTentacleVersion(string tentacleVersion)
        {
            this.tentacleVersion = tentacleVersion;

            return this;
        }

        public async Task<ClientAndTentacle> Build(CancellationToken cancellationToken)
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

            var tentacleClient = new TentacleClient(
                tentacleEndPoint,
                server.ServerHalibutRuntime,
                scriptObserverBackoffStrategy,
                tentacleServiceDecorator,
                retryDuration);

            return new ClientAndTentacle(server, portForwarder, runningTentacle, tentacleClient, temporaryDirectory);
        }
    }
}