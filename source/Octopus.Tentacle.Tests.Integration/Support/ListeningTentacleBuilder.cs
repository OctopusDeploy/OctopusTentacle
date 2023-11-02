using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ListeningTentacleBuilder : TentacleBuilder<ListeningTentacleBuilder>
    {
        public ListeningTentacleBuilder(string serverThumbprint)
        {
            ServerThumbprint = serverThumbprint;
        }

        internal async Task<RunningTentacle> Build(ILogger log, CancellationToken cancellationToken, string? customInstanceName = null)
        {
            var instanceName = customInstanceName ?? InstanceNameGenerator();
            var configFilePath = Path.Combine(HomeDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = TentacleExePath ?? TentacleExeFinder.FindTentacleExe();
            
            var logger = log.ForContext<ListeningTentacleBuilder>();
            logger.Information($"Tentacle.exe location: {tentacleExe}");

            await CreateInstance(tentacleExe, configFilePath, instanceName, HomeDirectory, cancellationToken);
            await AddCertificateToTentacle(tentacleExe, instanceName, CertificatePfxPath, HomeDirectory, cancellationToken);
            ConfigureTentacleToListen(configFilePath);

            var runningTentacle = await StartTentacle(
                null,
                tentacleExe,
                instanceName,
                HomeDirectory,
                TentacleThumbprint,
                logger,
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
