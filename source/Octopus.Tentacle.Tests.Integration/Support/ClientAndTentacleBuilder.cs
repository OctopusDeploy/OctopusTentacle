﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.TcpTentacleHelpers;
using Octopus.TestPortForwarder;
using Serilog;

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
        readonly List<Func<PortForwarderBuilder, PortForwarderBuilder>> portForwarderModifiers = new();
        readonly List<Action<ServiceEndPoint>> serviceEndpointModifiers = new();
        IPendingRequestQueueFactory? queueFactory = null;
        Reference<PortForwarder>? portForwarderReference;
        ITentacleClientObserver tentacleClientObserver = new NoTentacleClientObserver();
        Action<ITentacleBuilder>? tentacleBuilderAction;
        Action<TentacleClientOptions>? configureClientOptions;
        TcpConnectionUtilities? tcpConnectionUtilities;
        bool installAsAService = false;
        bool useDefaultMachineConfigurationHomeDirectory = false;
        HalibutTimeoutsAndLimits? halibutTimeoutsAndLimits;

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

        public ClientAndTentacleBuilder WithTcpConnectionUtilities(ILogger logger, out ITcpConnectionUtilities tcpConnectionUtilities)
        {
            this.tcpConnectionUtilities = new TcpConnectionUtilities(logger);
            tcpConnectionUtilities = this.tcpConnectionUtilities;

            return this;
        }

        public ClientAndTentacleBuilder WithTentacleClientObserver(ITentacleClientObserver tentacleClientObserver)
        {
            this.tentacleClientObserver = tentacleClientObserver;
            return this;
        }

        public ClientAndTentacleBuilder WithTentacle(Action<ITentacleBuilder> tentacleBuilderAction)
        {
            this.tentacleBuilderAction = tentacleBuilderAction;
            return this;
        }

        public ClientAndTentacleBuilder WithClientOptions(Action<TentacleClientOptions> configureClientOptions)
        {
            this.configureClientOptions = configureClientOptions;
            return this;
        }

        public ClientAndTentacleBuilder InstallAsAService()
        {
            installAsAService = true;

            return this;
        }

        public ClientAndTentacleBuilder UseDefaultMachineConfigurationHomeDirectory()
        {
            useDefaultMachineConfigurationHomeDirectory = true;

            return this;
        }

        public ClientAndTentacleBuilder WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
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
                .WithServerCertificate(TestCertificates.Server)
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits ?? HalibutTimeoutsAndLimits.RecommendedValues())
                .WithLegacyContractSupport();

            if (queueFactory != null)
            {
                serverHalibutRuntimeBuilder.WithPendingRequestQueueFactory(queueFactory);
            }

            var serverHalibutRuntime = serverHalibutRuntimeBuilder.Build();

            serverHalibutRuntime.Trust(TestCertificates.TentaclePublicThumbprint);
            var serverListeningPort = serverHalibutRuntime.Listen();

            var server = new Server(serverHalibutRuntime, serverListeningPort, TestCertificates.ServerPublicThumbprint, logger);

            // Port Forwarder
            PortForwarder? portForwarder;
            RunningTentacle runningTentacle;
            ServiceEndPoint tentacleEndPoint;

            var temporaryDirectory = new TemporaryDirectory();
            var tentacleExe = tentacleVersion == null ? TentacleExeFinder.FindTentacleExe(this.tentacleRuntime) : await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, tentacleRuntime, logger, cancellationToken);

            logger.Information($"Tentacle.exe location: {tentacleExe}");

            if (TentacleType == TentacleType.Polling)
            {
                portForwarder = BuildPortForwarder(serverListeningPort, null);

                var pollingTentacleBuilder = new PollingTentacleBuilder(portForwarder?.ListeningPort ?? serverListeningPort, TestCertificates.ServerPublicThumbprint, tentacleVersion)
                    .WithTentacleExe(tentacleExe);

                if (useDefaultMachineConfigurationHomeDirectory)
                {
                    pollingTentacleBuilder.UseDefaultMachineConfigurationHomeDirectory();
                }

                tentacleBuilderAction?.Invoke(pollingTentacleBuilder);

                if (installAsAService)
                {
                    pollingTentacleBuilder.InstallAsAService();
                }

                runningTentacle = await pollingTentacleBuilder.Build(logger, cancellationToken);

                tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint, serverHalibutRuntime.TimeoutsAndLimits);
            }
            else
            {
                var listeningTentacleBuilder = new ListeningTentacleBuilder(TestCertificates.ServerPublicThumbprint, tentacleVersion)
                    .WithTentacleExe(tentacleExe);

                if (useDefaultMachineConfigurationHomeDirectory)
                {
                    listeningTentacleBuilder.UseDefaultMachineConfigurationHomeDirectory();
                }

                tentacleBuilderAction?.Invoke(listeningTentacleBuilder);

                if (installAsAService)
                {
                    listeningTentacleBuilder.InstallAsAService();
                }

                runningTentacle = await listeningTentacleBuilder.Build(logger, cancellationToken);

                portForwarder = BuildPortForwarder(runningTentacle.ServiceUri.Port, null);

                tentacleEndPoint = new ServiceEndPoint(portForwarder?.PublicEndpoint ?? runningTentacle.ServiceUri, runningTentacle.Thumbprint, serverHalibutRuntime.TimeoutsAndLimits);
            }

            if (portForwarderReference != null && portForwarder != null)
            {
                portForwarderReference.Value = portForwarder;
            }

            foreach (var serviceEndpointModifier in serviceEndpointModifiers)
            {
                serviceEndpointModifier(tentacleEndPoint);
            }

            //make sure we do this after any service endpoint modifiers have run
            tcpConnectionUtilities?.Configure(server.ServerHalibutRuntime, tentacleEndPoint);

            TentacleClient.CacheServiceWasNotFoundResponseMessages(server.ServerHalibutRuntime);

            var retrySettings = new RpcRetrySettings(retriesEnabled, retryDuration);
            var clientOptions = new TentacleClientOptions(retrySettings);

            //configure the client options
            configureClientOptions?.Invoke(clientOptions);

            var tentacleClient = new TentacleClient(
                tentacleEndPoint,
                server.ServerHalibutRuntime,
                scriptObserverBackoffStrategy,
                tentacleClientObserver,
                clientOptions,
                tentacleServiceDecorator);

            return new ClientAndTentacle(server.ServerHalibutRuntime, tentacleEndPoint, server, portForwarder, runningTentacle, tentacleClient, temporaryDirectory, retrySettings, logger);
        }
    }
}
