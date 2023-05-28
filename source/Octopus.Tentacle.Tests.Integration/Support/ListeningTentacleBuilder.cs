using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ListeningTentacleBuilder : TentacleBuilder<ListeningTentacleBuilder>
    {
        public ListeningTentacleBuilder(string serverThumbprint)
        {
            ServerThumbprint = serverThumbprint;
        }

        internal async Task<RunningTentacle> Build(CancellationToken cancellationToken)
        {
            var tempDirectory = new TemporaryDirectory();
            var instanceName = Guid.NewGuid().ToString("N");
            var configFilePath = Path.Combine(tempDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = TentacleExePath ?? TentacleExeFinder.FindTentacleExe();

            CreateInstance(tentacleExe, configFilePath, instanceName, tempDirectory, cancellationToken);
            AddCertificateToTentacle(tentacleExe, instanceName, CertificatePfxPath, tempDirectory, cancellationToken);
            ConfigureTentacleToListen(configFilePath);

            var runningTentacle = await StartTentacle(
                null,
                tentacleExe,
                instanceName,
                tempDirectory,
                TentacleThumbprint,
                cancellationToken);

            SetThePort(configFilePath, runningTentacle.ServiceUri.Port);

            return runningTentacle;
        }

        private void ConfigureTentacleToListen(string configFilePath)
        {
            WithWritableTentacleConfiguration(configFilePath, writableTentacleConfiguration =>
            {
                writableTentacleConfiguration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration(ServerThumbprint)
                {
                    CommunicationStyle = CommunicationStyle.TentaclePassive,
                });

                writableTentacleConfiguration.SetApplicationDirectory(Path.Combine(new DirectoryInfo(configFilePath).Parent.FullName, "appdir"));

                writableTentacleConfiguration.SetServicesPortNumber(0); // Find a random available port
                writableTentacleConfiguration.SetNoListen(false);
            });
        }

        private void SetThePort(string configFilePath, int port)
        {
            WithWritableTentacleConfiguration(configFilePath, writableTentacleConfiguration =>
            {
                writableTentacleConfiguration.SetServicesPortNumber(port);
            });
        }
    }
}
