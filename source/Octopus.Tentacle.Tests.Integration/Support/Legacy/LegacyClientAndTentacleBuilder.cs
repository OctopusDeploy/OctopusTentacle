using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support.Logging;
using Octopus.Tentacle.Tests.Integration.Support.TentacleFetchers;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    class LegacyClientAndTentacleBuilder
    {
        readonly TentacleType tentacleType;
        Version? tentacleVersion;
        TentacleRuntime tentacleRuntime = DefaultTentacleRuntime.Value;
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
                .WithServerCertificate(TestCertificates.Server)
                .WithLegacyContractSupport()
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestBuilder().Build())
                .WithLogFactory(BuildClientLogger());
            
            var serverHalibutRuntime = serverHalibutRuntimeBuilder.Build();

            serverHalibutRuntime.Trust(TestCertificates.TentaclePublicThumbprint);
            var serverListeningPort = serverHalibutRuntime.Listen();

            var server = new Server(serverHalibutRuntime, serverListeningPort, TestCertificates.ServerPublicThumbprint, logger);

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

                runningTentacle = await new PollingTentacleBuilder(portForwarder.ListeningPort, TestCertificates.ServerPublicThumbprint, tentacleVersion)
                    .WithTentacleExe(tentacleExe)
                    .Build(logger, cancellationToken);

                tentacleEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint, serverHalibutRuntime.TimeoutsAndLimits);
            }
            else
            {
                runningTentacle = await new ListeningTentacleBuilder(TestCertificates.ServerPublicThumbprint, tentacleVersion)
                    .WithTentacleExe(tentacleExe)
                    .Build(logger, cancellationToken);

                portForwarder = new PortForwarderBuilder(runningTentacle.ServiceUri, new SerilogLoggerBuilder().Build()).Build();
                
                tentacleEndPoint = new ServiceEndPoint(portForwarder.PublicEndpoint, runningTentacle.Thumbprint, serverHalibutRuntime.TimeoutsAndLimits);
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
