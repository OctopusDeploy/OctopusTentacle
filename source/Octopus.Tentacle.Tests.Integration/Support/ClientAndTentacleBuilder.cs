using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ClientAndTentacleBuilder
    {
        ITentacleServiceDecorator? tentacleServiceDecorator;
        TimeSpan retryDuration = TimeSpan.FromMinutes(2);
        IScriptObserverBackoffStrategy scriptObserverBackoffStrategy = new DefaultScriptObserverBackoffStrategy();
        public readonly TentacleType TentacleType;
        string? tentacleVersion;
        readonly List<Func<PortForwarderBuilder, PortForwarderBuilder>> portForwarderModifiers = new ();
        readonly List<Action<ServiceEndPoint>> serviceEndpointModifiers = new();
        private IPendingRequestQueueFactory? queueFactory = null;
        private Reference<PortForwarder>? portForwarderReference;
        private ITentacleClientObserver tentacleClientObserver = new NoTentacleClientObserver();

        public ClientAndTentacleBuilder(TentacleType tentacleType)
        {
            this.TentacleType = tentacleType;
        }

        public ClientAndTentacleBuilder WithServiceEndpointModifier(Action<ServiceEndPoint> serviceEndpointModifier)
        {
            serviceEndpointModifiers.Add(serviceEndpointModifier);
            return this;
        }

        public ClientAndTentacleBuilder WithPendingRequestQueueFactory(IPendingRequestQueueFactory queueFactory)
        {
            this.queueFactory = queueFactory;
            return this;
        }

        internal ClientAndTentacleBuilder WithTentacleServiceDecorator(ITentacleServiceDecorator tentacleServiceDecorator)
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

        public ClientAndTentacleBuilder WithTentacleVersion(string? tentacleVersion)
        {
            this.tentacleVersion = tentacleVersion;

            return this;
        }

        public ClientAndTentacleBuilder WithPortForwarder()
        {
            return this.WithPortForwarder(p => p);
        }

        public ClientAndTentacleBuilder WithPortForwarder(Func<PortForwarderBuilder, PortForwarderBuilder> portForwarderBuilder)
        {
            this.portForwarderModifiers.Add(portForwarderBuilder);
            return this;
        }

        public ClientAndTentacleBuilder WithPortForwarder(out Reference<PortForwarder> portForwarder)
        {
            this.WithPortForwarder();

            this.portForwarderReference = new Reference<PortForwarder>();
            portForwarder = this.portForwarderReference;

            return this;
        }

        public ClientAndTentacleBuilder WithTentacleClientObserver(ITentacleClientObserver tentacleClientObserver)
        {
            this.tentacleClientObserver = tentacleClientObserver;
            return this;
        }

        private PortForwarder? BuildPortForwarder(int localPort, int? listeningPort)
        {
            if (portForwarderModifiers.Count == 0) return null;

            return portForwarderModifiers.Aggregate(
                    PortForwarderBuilder
                        .ForwardingToLocalPort(localPort, new SerilogLoggerBuilder().Build())
                        .ListenOnPort(listeningPort),
                    (current, portForwarderModifier) => portForwarderModifier(current))
                .Build();
        }

        public async Task<ClientAndTentacle> Build(CancellationToken cancellationToken)
        {
            // Server
            var serverHalibutRuntimeBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Server)
                .WithLegacyContractSupport();
            if (queueFactory != null) serverHalibutRuntimeBuilder.WithPendingRequestQueueFactory(queueFactory);
            var serverHalibutRuntime = serverHalibutRuntimeBuilder.Build();

            serverHalibutRuntime.Trust(Certificates.TentaclePublicThumbprint);
            var serverListeningPort = serverHalibutRuntime.Listen();

            var server = new Server(serverHalibutRuntime, serverListeningPort);

            // Port Forwarder
            PortForwarder? portForwarder;
            RunningTentacle runningTentacle;
            ServiceEndPoint tentacleEndPoint;

            var temporaryDirectory = new TemporaryDirectory();
            var tentacleExe = string.IsNullOrWhiteSpace(tentacleVersion) ?
                TentacleExeFinder.FindTentacleExe() :
                await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, cancellationToken);

            if (TentacleType == TentacleType.Polling)
            {
                portForwarder = BuildPortForwarder(serverListeningPort, null);

                runningTentacle = await new PollingTentacleBuilder(portForwarder?.ListeningPort ?? serverListeningPort, Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(cancellationToken);

                tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);
            }
            else
            {
                runningTentacle = await new ListeningTentacleBuilder(Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(cancellationToken);

                portForwarder = BuildPortForwarder(runningTentacle.ServiceUri.Port, null);

                tentacleEndPoint = new ServiceEndPoint(portForwarder?.PublicEndpoint ?? runningTentacle.ServiceUri, runningTentacle.Thumbprint);
            }

            if (portForwarderReference != null && portForwarder != null)
            {
                portForwarderReference.Value = portForwarder;
            }

            foreach (var serviceEndpointModifier in serviceEndpointModifiers)
            {
                serviceEndpointModifier(tentacleEndPoint);
            }

            var tentacleClient = new TentacleClient(
                tentacleEndPoint,
                server.ServerHalibutRuntime,
                scriptObserverBackoffStrategy,
                retryDuration,
                tentacleClientObserver,
                tentacleServiceDecorator);

            return new ClientAndTentacle(server.ServerHalibutRuntime, tentacleEndPoint, server, portForwarder, runningTentacle, tentacleClient, temporaryDirectory);
        }
    }
}