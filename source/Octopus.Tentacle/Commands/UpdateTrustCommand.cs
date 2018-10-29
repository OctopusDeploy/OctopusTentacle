﻿using System;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Commands
{
    public class UpdateTrustCommand : AbstractStandardCommand
    {
        readonly Lazy<ITentacleConfiguration> tentacleConfiguration;
        readonly ILog log;
        string oldThumbprint;
        string newThumbprint;

        public UpdateTrustCommand(
            Lazy<ITentacleConfiguration> tentacleConfiguration,
            ILog log,
            IApplicationInstanceSelector selector)
            : base(selector)
        {
            this.tentacleConfiguration = tentacleConfiguration;
            this.log = log;

            Options.Add("oldThumbprint=", "The thumbprint of the old Octopus Server to be replaced", v => oldThumbprint = v);
            Options.Add("newThumbprint=", "The thumbprint of the new Octopus Server", v => newThumbprint = v);
        }

        protected void CheckArgs()
        {
            if (string.IsNullOrWhiteSpace(oldThumbprint))
                throw new ControlledFailureException("Please specify an thumbprint to replace, e.g., --oldThumbprint=VALUE");

            if (string.IsNullOrWhiteSpace(newThumbprint))
                throw new ControlledFailureException("Please specify a new thumbprint, e.g., --newThumbprint=VALUE");
        }

        protected override void Start()
        {
            CheckArgs();

            log.Info($"Updating Octopus servers thumbprints from {oldThumbprint} to {newThumbprint}");
            tentacleConfiguration.Value.UpdateTrustedServerThumbprint(oldThumbprint, newThumbprint);

            VoteForRestart();
        }
    }
}
