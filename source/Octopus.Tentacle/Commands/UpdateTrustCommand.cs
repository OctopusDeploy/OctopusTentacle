using System;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class UpdateTrustCommand : AbstractStandardCommand
    {
        readonly Lazy<IWritableTentacleConfiguration> tentacleConfiguration;
        readonly ISystemLog log;
        string oldThumbprint;
        string newThumbprint;

        public UpdateTrustCommand(
            Lazy<IWritableTentacleConfiguration> tentacleConfiguration,
            ISystemLog log,
            IApplicationInstanceSelector selector)
            : base(selector, log)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("oldThumbprint=", "The thumbprint of the old Octopus Server to be replaced", v => oldThumbprint = v);
            Options.Add("newThumbprint=", "The thumbprint of the new Octopus Server", v => newThumbprint = v);
        }

        protected void CheckArgs()
        {
            if (string.IsNullOrWhiteSpace(oldThumbprint))
                throw new ControlledFailureException("Please specify a thumbprint to replace, e.g., --oldThumbprint=VALUE");

            if (string.IsNullOrWhiteSpace(newThumbprint))
                throw new ControlledFailureException("Please specify a new thumbprint to trust, e.g., --newThumbprint=VALUE");
        }

        protected override void Start()
        {
            base.Start();
            CheckArgs();

            log.Info($"Updating Octopus servers thumbprint from {oldThumbprint} to {newThumbprint}");
            tentacleConfiguration.Value.UpdateTrustedServerThumbprint(oldThumbprint, newThumbprint);

            VoteForRestart();
        }
    }
}
