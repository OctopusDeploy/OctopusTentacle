﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Util.TcpUtils;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ClientAndTentacleBuilder
    {
        ITentacleServiceDecorator? tentacleServiceDecorator;
        TimeSpan retryDuration = TimeSpan.FromMinutes(2);
        IScriptObserverBackoffStrategy scriptObserverBackoffStrategy = new DefaultScriptObserverBackoffStrategy();
        readonly TentacleType tentacleType;
        string? tentacleVersion;
        readonly List<Func<PortForwarderBuilder, PortForwarderBuilder>> portForwarderBuilderFunc = new ();
        readonly List<Action<ServiceEndPoint>> serviceEndpointModifiers = new();
        private IPendingRequestQueueFactory? queueFactory = null;

        public ClientAndTentacleBuilder(TentacleType tentacleType)
        {
            this.tentacleType = tentacleType;
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

        public ClientAndTentacleBuilder WithPortForwarder(Func<PortForwarderBuilder, PortForwarderBuilder> portForwarderBuilder)
        {
            this.portForwarderBuilderFunc.Add(portForwarderBuilder);
            return this;
        }

        private PortForwarder BuildPortForwarder(int port)
        {
            return portForwarderBuilderFunc.Aggregate(PortForwarderBuilder.ForwardingToLocalPort(port), (current, port) => port(current)).Build();
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
            PortForwarder portForwarder;
            RunningTentacle runningTentacle;
            ServiceEndPoint tentacleEndPoint;

            var temporaryDirectory = new TemporaryDirectory();
            var tentacleExe = string.IsNullOrWhiteSpace(tentacleVersion) ?
                TentacleExeFinder.FindTentacleExe() :
                await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion);

            if (tentacleType == TentacleType.Polling)
            {
                portForwarder = BuildPortForwarder(serverListeningPort);

                runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(cancellationToken);

                tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);
            }
            else
            {
                runningTentacle = await new ListeningTentacleBuilder(Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(cancellationToken);

                portForwarder = BuildPortForwarder(runningTentacle.ServiceUri.Port);

                tentacleEndPoint = new ServiceEndPoint(portForwarder.PublicEndpoint, runningTentacle.Thumbprint);
            }
            
            
            foreach (var serviceEndpointModifier in serviceEndpointModifiers)
            {
                serviceEndpointModifier(tentacleEndPoint);
            }

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