using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ListeningRunningTentacleBuilder : RunningTentacleBuilder<ListeningRunningTentacleBuilder>
    {
        internal ListeningRunningTentacleBuilder(string serverThumbprint, TentacleInstanceBuilder instanceBuilder) : base(instanceBuilder)
        {
            ServerThumbprint = serverThumbprint;
        }

        public ListeningRunningTentacleBuilder(string serverThumbprint, TentacleInstance instance) : base(instance)
        {
            ServerThumbprint = serverThumbprint;
        }

        internal async Task<RunningTentacle> Build(ILogger log, CancellationToken cancellationToken)
        {
            var logger = log.ForContext<ListeningRunningTentacleBuilder>();

            using (await GetConfigureAndStartTentacleLockIfRequired(logger, cancellationToken))
            {
                var instance = await BuildInstance(logger, cancellationToken);
                logger.Information($"Tentacle.exe location: {instance.TentacleExe}");

                var applicationDirectory = Path.Combine(instance.Home.DirectoryPath, "appdir");
                ConfigureTentacleToListen(instance.ConfigurationFile, applicationDirectory);

                var runningTentacle = await StartTentacle(null, instance, applicationDirectory, logger, cancellationToken);

                SetThePort(instance.ConfigurationFile, runningTentacle.ServiceUri.Port);

                return runningTentacle;
            }
        }

        private void ConfigureTentacleToListen(string configFilePath, string applicationDirectory)
        {
            WithWritableTentacleConfiguration(configFilePath, writableTentacleConfiguration =>
            {
                writableTentacleConfiguration.AddOrUpdateTrustedOctopusServer(new OctopusServerConfiguration(ServerThumbprint)
                {
                    CommunicationStyle = CommunicationStyle.TentaclePassive,
                });

                writableTentacleConfiguration.SetApplicationDirectory(applicationDirectory);

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
