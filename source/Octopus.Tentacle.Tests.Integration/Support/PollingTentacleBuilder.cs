using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class PollingTentacleBuilder : TentacleBuilder<PollingTentacleBuilder>
    {
        readonly int pollingPort;

        public PollingTentacleBuilder(int pollingPort, string serverThumbprint)
        {
            this.pollingPort = pollingPort;

            ServerThumbprint = serverThumbprint;
        }
        
        internal async Task<RunningTentacle> Build(ILogger log, CancellationToken cancellationToken)
        {
            var instanceName = InstanceNameGenerator();
            var configFilePath = Path.Combine(HomeDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = TentacleExePath ?? TentacleExeFinder.FindTentacleExe();
            var subscriptionId = PollingSubscriptionId.Generate();
            
            var logger = log.ForContext<PollingTentacleBuilder>();
            logger.Information($"Tentacle.exe location: {tentacleExe}");

            ConfigureTentacleMachineConfigurationHomeDirectory();
            await CreateInstance(tentacleExe, configFilePath, instanceName, HomeDirectory, logger, cancellationToken);
            var applicationDirectory = Path.Combine(HomeDirectory.DirectoryPath, "appdir");
            ConfigureTentacleToPollOctopusServer(configFilePath, subscriptionId, applicationDirectory);
            await AddCertificateToTentacle(tentacleExe, instanceName, CertificatePfxPath, HomeDirectory, logger,cancellationToken);
            
            return await StartTentacle(
                subscriptionId,
                tentacleExe,
                instanceName,
                HomeDirectory,
                applicationDirectory,
                TentacleThumbprint,
                logger,
                cancellationToken);
        }

        void ConfigureTentacleToPollOctopusServer(string configFilePath, Uri subscriptionId, string applicationDirectory)
        {
            WithWritableTentacleConfiguration(configFilePath, writableTentacleConfiguration =>
            {
                writableTentacleConfiguration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration(ServerThumbprint)
                {
                    Address = new Uri("https://localhost:" + pollingPort),
                    CommunicationStyle = CommunicationStyle.TentacleActive,
                    SubscriptionId = subscriptionId.ToString()
                });

                writableTentacleConfiguration.SetApplicationDirectory(applicationDirectory);
                writableTentacleConfiguration.SetNoListen(true);
            });
        }
    }
}
