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

        internal async Task<RunningTentacle> Build(ILogger log, CancellationToken cancellationToken, string? customInstanceName = null)
        {
            var instanceName = customInstanceName ?? InstanceNameGenerator();
            var configFilePath = Path.Combine(HomeDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = TentacleExePath ?? TentacleExeFinder.FindTentacleExe();
            var subscriptionId = PollingSubscriptionId.Generate();
            
            var logger = log.ForContext<PollingTentacleBuilder>();
            logger.Information($"Tentacle.exe location: {tentacleExe}");

            await CreateInstance(tentacleExe, configFilePath, instanceName, HomeDirectory, cancellationToken);
            ConfigureTentacleToPollOctopusServer(configFilePath, subscriptionId);
            await AddCertificateToTentacle(tentacleExe, instanceName, CertificatePfxPath, HomeDirectory, cancellationToken);
            

            return await StartTentacle(
                subscriptionId,
                tentacleExe,
                instanceName,
                HomeDirectory,
                TentacleThumbprint,
                logger,
                cancellationToken);
        }

        private void ConfigureTentacleToPollOctopusServer(string configFilePath, Uri subscriptionId)
        {
            WithWritableTentacleConfiguration(configFilePath, writableTentacleConfiguration =>
            {
                writableTentacleConfiguration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration(ServerThumbprint)
                {
                    Address = new Uri("https://localhost:" + pollingPort),
                    CommunicationStyle = CommunicationStyle.TentacleActive,
                    SubscriptionId = subscriptionId.ToString()
                });

                writableTentacleConfiguration.SetApplicationDirectory(Path.Combine(new DirectoryInfo(configFilePath).Parent.FullName, "appdir"));
                writableTentacleConfiguration.SetNoListen(true);
            });
        }
    }
}
