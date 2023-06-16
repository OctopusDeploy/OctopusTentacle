using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class PollingTentacleBuilder : TentacleBuilder<PollingTentacleBuilder>
    {
        readonly int pollingPort;

        public PollingTentacleBuilder(int pollingPort, string serverThumbprint, TestCertificate tentacleCertificate) : base(tentacleCertificate)
        {
            this.pollingPort = pollingPort;

            ServerThumbprint = serverThumbprint;
        }

        internal async Task<RunningTentacle> Build(CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = InstanceNameGenerator();
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = TentacleExePath ?? TentacleExeFinder.FindTentacleExe();
            var subscriptionId = PollingSubscriptionId.Generate();

            CreateInstance(tentacleExe, configFilePath, instanceName, tempDirectory, cancellationToken);
            ConfigureTentacleToPollOctopusServer(configFilePath, subscriptionId);
            AddCertificateToTentacle(tentacleExe, instanceName, tentacleCertificate.PfxPath, tempDirectory, cancellationToken);
            

            return await StartTentacle(
                subscriptionId,
                tentacleExe,
                instanceName,
                tempDirectory,
                tentacleCertificate.Thumbprint,
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
