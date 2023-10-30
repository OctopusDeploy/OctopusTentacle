using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Halibut.Util;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support.Logging;
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
        LogLevel halibutLogLevel = LogLevel.Info;

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
        
        public LegacyClientAndTentacleBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            this.halibutLogLevel = halibutLogLevel;
            return this;
        }

        public async Task<LegacyClientAndTentacle> Build(CancellationToken cancellationToken)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<LegacyTentacleClientBuilder>();
            // Server
            var serverHalibutRuntimeBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(Certificates.Server)
                .WithLegacyContractSupport()
                .WithLogFactory(BuildClientLogger());
            
            var serverHalibutRuntime = serverHalibutRuntimeBuilder.Build();

            serverHalibutRuntime.Trust(Certificates.TentaclePublicThumbprint);
            var serverListeningPort = serverHalibutRuntime.Listen();

            var server = new Server(serverHalibutRuntime, serverListeningPort, logger);

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
                    .Build(logger, cancellationToken);

#pragma warning disable CS0612
                tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);
#pragma warning restore CS0612
            }
            else
            {
                runningTentacle = await new ListeningTentacleBuilder(Certificates.ServerPublicThumbprint)
                    .WithTentacleExe(tentacleExe)
                    .Build(logger, cancellationToken);

                portForwarder = new PortForwarderBuilder(runningTentacle.ServiceUri, new SerilogLoggerBuilder().Build()).Build();

#pragma warning disable CS0612
                tentacleEndPoint = new ServiceEndPoint(portForwarder.PublicEndpoint, runningTentacle.Thumbprint);
#pragma warning restore CS0612
            }

            var tentacleClient = new LegacyTentacleClientBuilder(server.ServerHalibutRuntime, tentacleEndPoint)
                .Build();

            return new LegacyClientAndTentacle(server, portForwarder, runningTentacle, tentacleClient, temporaryDirectory, logger);
        }

        ILogFactory BuildClientLogger()
        {
            return new TestContextLogCreator("Client", halibutLogLevel).ToCachingLogFactory();
        }
    }
}
