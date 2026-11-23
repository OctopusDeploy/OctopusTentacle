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
    public class PollingRunningTentacleBuilder : RunningTentacleBuilder<PollingRunningTentacleBuilder>
    {
        readonly int pollingPort;
        Uri? subscriptionId;

        internal PollingRunningTentacleBuilder(int pollingPort, string serverThumbprint, TentacleInstanceBuilder instanceBuilder) : base(instanceBuilder)
        {
            this.pollingPort = pollingPort;
            ServerThumbprint = serverThumbprint;
        }

        public PollingRunningTentacleBuilder(int pollingPort, string serverThumbprint, TentacleInstance instance) : base(instance)
        {
            this.pollingPort = pollingPort;
            ServerThumbprint = serverThumbprint;
        }

        public PollingRunningTentacleBuilder WithSubscriptionId(Uri subscriptionId)
        {
            this.subscriptionId = subscriptionId;
            return this;
        }

        internal async Task<RunningTentacle> Build(ILogger log, CancellationToken cancellationToken)
        {
            var subscriptionId = this.subscriptionId ?? PollingSubscriptionId.Generate();
            var logger = log.ForContext<PollingRunningTentacleBuilder>();

            using (await GetConfigureAndStartTentacleLockIfRequired(logger, cancellationToken))
            {
                var instance = await BuildInstance(logger, cancellationToken);
                logger.Information($"Tentacle.exe location: {instance.TentacleExe}");

                var applicationDirectory = Path.Combine(instance.Home.DirectoryPath, "appdir");
                ConfigureTentacleToPollOctopusServer(instance.ConfigurationFile, subscriptionId, applicationDirectory);

                return await StartTentacle(subscriptionId, instance, applicationDirectory, logger, cancellationToken);
            }
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
