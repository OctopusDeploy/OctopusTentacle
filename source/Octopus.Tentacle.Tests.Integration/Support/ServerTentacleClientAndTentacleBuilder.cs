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
    internal class ServerTentacleClientAndTentacleBuilder
    {
        private ITentacleServiceDecorator? tentacleServiceDecorator;
        private TimeSpan retryDuration = TimeSpan.FromMinutes(2);
        private IScriptObserverBackoffStrategy scriptObserverBackoffStrategy = new DefaultScriptObserverBackoffStrategy();
        private readonly TentacleType tentacleType;

        public ServerTentacleClientAndTentacleBuilder(TentacleType tentacleType)
        {
            this.tentacleType = tentacleType;
        }

        public ServerTentacleClientAndTentacleBuilder WithTentacleServiceDecorator(ITentacleServiceDecorator tentacleServiceDecorator)
        {
            this.tentacleServiceDecorator = tentacleServiceDecorator;

            return this;
        }

        public ServerTentacleClientAndTentacleBuilder WithRetryDuration(TimeSpan retryDuration)
        {
            this.retryDuration = retryDuration;

            return this;
        }

        public ServerTentacleClientAndTentacleBuilder WithScriptObserverBackoffStrategy(IScriptObserverBackoffStrategy scriptObserverBackoffStrategy)
        {
            this.scriptObserverBackoffStrategy = scriptObserverBackoffStrategy;

            return this;
        }

        public async Task<(
            Server server,
            PortForwarder portForwarder,
            RunningTentacle runningTentacle,
            ITentacleClient tentacleClient)>
            Build(CancellationToken cancellationToken)
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

            if (tentacleType == TentacleType.Polling)
            {
                runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Certificates.ServerPublicThumbprint)
                    .Build(cancellationToken);
            }
            else
            {
                runningTentacle = await new ListeningTentacleBuilder(Certificates.ServerPublicThumbprint)
                    .Build(cancellationToken);
            }

            var tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

            var tentacleClient = new TentacleClient(
                tentacleEndPoint,
                server.ServerHalibutRuntime,
                scriptObserverBackoffStrategy,
                tentacleServiceDecorator,
                retryDuration);

            return (server, portForwarder, runningTentacle, tentacleClient);
        }
    }
}