using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Halibut.Util;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ClientAndTentacleBuilder
    {
        ITentacleServiceDecorator? tentacleServiceDecorator;
        TimeSpan retryDuration = TimeSpan.FromMinutes(2);
        bool retriesEnabled = true;
        IScriptObserverBackoffStrategy scriptObserverBackoffStrategy = new DefaultScriptObserverBackoffStrategy();
        public readonly TentacleType TentacleType;
        Version? tentacleVersion;
        readonly List<Func<PortForwarderBuilder, PortForwarderBuilder>> portForwarderModifiers = new ();
        readonly List<Action<ServiceEndPoint>> serviceEndpointModifiers = new();
        private IPendingRequestQueueFactory? queueFactory = null;
        private Reference<PortForwarder>? portForwarderReference;
        private ITentacleClientObserver tentacleClientObserver = new NoTentacleClientObserver();
        private AsyncHalibutFeature asyncHalibutFeature = AsyncHalibutFeature.Disabled;

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

        public ClientAndTentacleBuilder WithRetriesDisabled()
        {
            this.retriesEnabled = false;
            return this;
        }

        public ClientAndTentacleBuilder WithScriptObserverBackoffStrategy(IScriptObserverBackoffStrategy scriptObserverBackoffStrategy)
        {
            this.scriptObserverBackoffStrategy = scriptObserverBackoffStrategy;

            return this;
        }

        public ClientAndTentacleBuilder WithTentacleVersion(Version? tentacleVersion)
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

        public ClientAndTentacleBuilder WithAsyncHalibutFeature(AsyncHalibutFeature asyncHalibutFeature)
        {
            this.asyncHalibutFeature = asyncHalibutFeature;
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
            var logger = new SerilogLoggerBuilder().Build().ForContext<ClientAndTentacleBuilder>();
            // Server
            var serverHalibutRuntimeBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Server)
                .WithLegacyContractSupport();

            if (asyncHalibutFeature.IsEnabled())
            {
                serverHalibutRuntimeBuilder.WithAsyncHalibutFeatureEnabled();
            }

            if (queueFactory != null)
            {
                serverHalibutRuntimeBuilder.WithPendingRequestQueueFactory(queueFactory);
            }

            var serverHalibutRuntime = serverHalibutRuntimeBuilder.Build();

            serverHalibutRuntime.Trust(Certificates.TentaclePublicThumbprint);
            var serverListeningPort = serverHalibutRuntime.Listen();

            var server = new Server(serverHalibutRuntime, serverListeningPort);

            // Port Forwarder
            PortForwarder? portForwarder;
            RunningTentacle runningTentacle;
            ServiceEndPoint tentacleEndPoint;

            var temporaryDirectory = new TemporaryDirectory();
            var tentacleExe = tentacleVersion == null ?
                TentacleExeFinder.FindTentacleExe() :
                await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, logger, cancellationToken);

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

            TentacleClient.CacheServiceWasNotFoundResponseMessages(server.ServerHalibutRuntime);

            var tentacleClient = new TentacleClient(
                tentacleEndPoint,
                server.ServerHalibutRuntime,
                scriptObserverBackoffStrategy,
                tentacleClientObserver,
                new RpcRetrySettings(retriesEnabled, retryDuration),
                tentacleServiceDecorator);

            return new ClientAndTentacle(server.ServerHalibutRuntime, tentacleEndPoint, server, portForwarder, runningTentacle, tentacleClient, temporaryDirectory);
        }
    }
}
