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

        internal async Task<RunningTentacle> Build(ILogger log, CancellationToken cancellationToken)
        {
            var instanceName = InstanceNameGenerator();
            //Path.Combine(HomeDirectory.DirectoryPath, instanceName + ".cfg");
            var tentacleExe = TentacleExePath ?? TentacleExeFinder.FindTentacleExe();
            
            var logger = log.ForContext<ListeningTentacleBuilder>();
            logger.Information($"Tentacle.exe location: {tentacleExe}");

            var tempDirectory = Path.Combine(Path.GetTempPath(), instanceName);
            Directory.CreateDirectory(tempDirectory);

            var configFilePath = Path.Combine(tempDirectory, "Tentacle.config");

            var exePath = new FileInfo(tentacleExe);
            CopyDirectory(exePath.Directory.FullName, tempDirectory, true);

            tentacleExe = Path.Combine(tempDirectory, exePath.Name);

            await CreateInstance(tentacleExe, configFilePath, instanceName, false, HomeDirectory, cancellationToken);
            await AddCertificateToTentacle(tentacleExe, instanceName, CertificatePfxPath, false, HomeDirectory, cancellationToken);

            ConfigureTentacleToListen(configFilePath);

            var runningTentacle = await StartTentacle(
                null,
                tentacleExe,
                instanceName,
                false,
                HomeDirectory,
                TentacleThumbprint,
                logger,
                cancellationToken);

            SetThePort(configFilePath, runningTentacle.ServiceUri.Port);

            return runningTentacle;

            static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
            {
                // Get information about the source directory
                var dir = new DirectoryInfo(sourceDir);

                // Check if the source directory exists
                if (!dir.Exists)
                    throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

                // Cache directories before we start copying
                DirectoryInfo[] dirs = dir.GetDirectories();

                // Create the destination directory
                Directory.CreateDirectory(destinationDir);

                // Get the files in the source directory and copy to the destination directory
                foreach (FileInfo file in dir.GetFiles())
                {
                    string targetFilePath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(targetFilePath);
                }

                // If recursive and copying subdirectories, recursively call this method
                if (recursive)
                {
                    foreach (DirectoryInfo subDir in dirs)
                    {
                        string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                        CopyDirectory(subDir.FullName, newDestinationDir, true);
                    }
                }
            }
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
