using System;
using System.Linq;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    public class ClearTrustedServersCommand : AbstractStandardCommand
    {
        readonly Lazy<IWritableTentacleConfiguration> configuration;
        string[] excludedServerCommsAddresses = Array.Empty<string>();

        public ClearTrustedServersCommand(
            Lazy<IWritableTentacleConfiguration> configuration,
            IApplicationInstanceSelector instanceSelector,
            ISystemLog systemLog,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, systemLog, logFileOnlyLogger)
        {
            this.configuration = configuration;
            Options.Add("keep=", "A comma separated list of Server Comms Addresses to keep as trusted servers",
                s => excludedServerCommsAddresses = s.Split(','));
        }

        protected override void Start()
        {
            // When http: serverCommsAddresses are added they are converted
            // to https: here: RegisterMachineCommandBase<TRegistrationOperationType>.GetActiveTentacleAddress
            // Adjusting the address here to match.
            var adjustedExcludedServerCommsAddresses =
                excludedServerCommsAddresses.Select(a => a.Replace("http://", "https://")).ToHashSet();
            var serversToKeep = configuration.Value.TrustedOctopusServers
                .Where(s => adjustedExcludedServerCommsAddresses.Contains(s.Address.ToString())).ToList();

            configuration.Value.ResetTrustedOctopusServers();
            configuration.Value.SetTrustedOctopusServers(serversToKeep);
        }
    }
}