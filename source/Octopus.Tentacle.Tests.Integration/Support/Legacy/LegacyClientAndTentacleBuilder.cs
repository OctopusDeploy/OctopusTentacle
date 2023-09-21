﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Util;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    internal class LegacyClientAndTentacleBuilder
    {
        private readonly TentacleType tentacleType;
        private Version? tentacleVersion;
        private TentacleRuntime tentacleRuntime = DefaultTentacleRuntime.Value;
        private AsyncHalibutFeature asyncHalibutFeature = AsyncHalibutFeature.Disabled;

        public LegacyClientAndTentacleBuilder(TentacleType tentacleType)
        {
            this.tentacleType = tentacleType;
        }

        public LegacyClientAndTentacleBuilder WithTentacleVersion(Version? tentacleVersion)
        {
            this.tentacleVersion = tentacleVersion;
            return this;
        }

        public LegacyClientAndTentacleBuilder WithTentacleRuntime(TentacleRuntime tentacleRuntime)
        {
            this.tentacleRuntime = tentacleRuntime;
            return this;
        }

        public LegacyClientAndTentacleBuilder WithAsyncHalibutFeature(AsyncHalibutFeature asyncHalibutFeature)
        {
            this.asyncHalibutFeature = asyncHalibutFeature;

            return this;
        }

        public async Task<LegacyClientAndTentacle> Build(CancellationToken cancellationToken)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<LegacyTentacleClientBuilder>();
            // Server
            var serverHalibutRuntimeBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Server)
                .WithLegacyContractSupport();

            if (asyncHalibutFeature.IsEnabled())
            {
                serverHalibutRuntimeBuilder.WithAsyncHalibutFeatureEnabled();
            }

            var serverHalibutRuntime = serverHalibutRuntimeBuilder.Build();

            serverHalibutRuntime.Trust(Certificates.TentaclePublicThumbprint);
            var serverListeningPort = serverHalibutRuntime.Listen();

            var server = new Server(serverHalibutRuntime, serverListeningPort);

            // Port Forwarder
            PortForwarder portForwarder;
            RunningTentacle runningTentacle;
            ServiceEndPoint tentacleEndPoint;

            // Tentacle
            var temporaryDirectory = new TemporaryDirectory();
            var tentacleExe = tentacleVersion == null ?
                TentacleExeFinder.FindTentacleExe(this.tentacleRuntime) :
                await TentacleFetcher.GetTentacleVersion(temporaryDirectory.DirectoryPath, tentacleVersion, tentacleRuntime, logger, cancellationToken);
            
            logger.Information($"Tentacle.exe location: {tentacleExe}");

            if (tentacleType == TentacleType.Polling)
            {
                portForwarder = PortForwarderBuilder.ForwardingToLocalPort(serverListeningPort, new SerilogLoggerBuilder().Build()).Build();

                runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(cancellationToken);

#pragma warning disable CS0612
                tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);
#pragma warning restore CS0612
            }
            else
            {
                runningTentacle = await new ListeningTentacleBuilder(Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(cancellationToken);

                portForwarder = new PortForwarderBuilder(runningTentacle.ServiceUri, new SerilogLoggerBuilder().Build()).Build();

#pragma warning disable CS0612
                tentacleEndPoint = new ServiceEndPoint(portForwarder.PublicEndpoint, runningTentacle.Thumbprint);
#pragma warning restore CS0612
            }

            var tentacleClient = new LegacyTentacleClientBuilder(server.ServerHalibutRuntime, tentacleEndPoint, asyncHalibutFeature)
                .Build(cancellationToken);

            return new LegacyClientAndTentacle(server, portForwarder, runningTentacle, tentacleClient, temporaryDirectory);
        }
    }
}
