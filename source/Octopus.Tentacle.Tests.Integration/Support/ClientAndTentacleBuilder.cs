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
        ITentacleServiceDecoratorFactory? tentacleServiceDecorator;
        TimeSpan retryDuration = TimeSpan.FromMinutes(2);
        bool retriesEnabled = true;
        IScriptObserverBackoffStrategy scriptObserverBackoffStrategy = new DefaultScriptObserverBackoffStrategy();
        public readonly TentacleType TentacleType;
        Version? tentacleVersion;
        TentacleRuntime tentacleRuntime = DefaultTentacleRuntime.Value;
        readonly List<Func<PortForwarderBuilder, PortForwarderBuilder>> portForwarderModifiers = new ();
        readonly List<Action<ServiceEndPoint>> serviceEndpointModifiers = new();
        IPendingRequestQueueFactory? queueFactory = null;
        Reference<PortForwarder>? portForwarderReference;
        ITentacleClientObserver tentacleClientObserver = new NoTentacleClientObserver();
        AsyncHalibutFeature asyncHalibutFeature = AsyncHalibutFeature.Disabled;
        Action<ITentacleBuilder>? tentacleBuilderAction;

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

        internal ClientAndTentacleBuilder WithTentacleServiceDecorator(ITentacleServiceDecoratorFactory tentacleServiceDecoratorFactory)
        {
            this.tentacleServiceDecorator = tentacleServiceDecoratorFactory;

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

        public ClientAndTentacleBuilder WithTentacleRuntime(TentacleRuntime tentacleRuntime)
        {
            this.tentacleRuntime = tentacleRuntime;
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
        
        public ClientAndTentacleBuilder WithTentacle(Action<ITentacleBuilder> tentacleBuilderAction)
        {
            this.tentacleBuilderAction = tentacleBuilderAction;
            return this;
        }

        PortForwarder? BuildPortForwarder(int localPort, int? listeningPort)
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

            var server = new Server(serverHalibutRuntime, serverListeningPort, logger);

            // Port Forwarder
            PortForwarder? portForwarder;
            RunningTentacle runningTentacle;
            ServiceEndPoint tentacleEndPoint;

            var temporaryDirectory = new TemporaryDirectory();
            var tentacleExe = tentacleVersion == null ?
                TentacleExeFinder.FindTentacleExe(this.tentacleRuntime) :
                await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, tentacleRuntime, logger, cancellationToken);
            
            logger.Information($"Tentacle.exe location: {tentacleExe}");

            if (TentacleType == TentacleType.Polling)
            {
                portForwarder = BuildPortForwarder(serverListeningPort, null);

                var pollingTentacleBuilder = new PollingTentacleBuilder(portForwarder?.ListeningPort ?? serverListeningPort, Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe);

                tentacleBuilderAction?.Invoke(pollingTentacleBuilder);

                runningTentacle = await pollingTentacleBuilder.Build(logger, cancellationToken);

#pragma warning disable CS0612
                tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);
#pragma warning restore CS0612
            }
            else
            {
                var listeningTentacleBuilder = new ListeningTentacleBuilder(Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe);

                tentacleBuilderAction?.Invoke(listeningTentacleBuilder);

                runningTentacle = await listeningTentacleBuilder.Build(logger, cancellationToken);

                portForwarder = BuildPortForwarder(runningTentacle.ServiceUri.Port, null);

#pragma warning disable CS0612
                tentacleEndPoint = new ServiceEndPoint(portForwarder?.PublicEndpoint ?? runningTentacle.ServiceUri, runningTentacle.Thumbprint);
#pragma warning restore CS0612
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

            var retrySettings = new RpcRetrySettings(retriesEnabled, retryDuration);

            var tentacleClient = new TentacleClient(
                tentacleEndPoint,
                server.ServerHalibutRuntime,
                scriptObserverBackoffStrategy,
                tentacleClientObserver,
                retrySettings,
                tentacleServiceDecorator);

            return new ClientAndTentacle(server.ServerHalibutRuntime, tentacleEndPoint, server, portForwarder, runningTentacle, tentacleClient, temporaryDirectory, retrySettings, logger);
        }
    }
}
