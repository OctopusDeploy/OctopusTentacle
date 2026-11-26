using System;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class UpgradeConfigurationEncryptionCommand : AbstractStandardCommand
    {
        readonly Lazy<IConfigurationEncryptionUpgrade> configurationEncryptionUpgrade;
        readonly IApplicationInstanceSelector instanceSelector;
        readonly ISystemLog log;

        public UpgradeConfigurationEncryptionCommand(
            Lazy<IConfigurationEncryptionUpgrade> configurationEncryptionUpgrade,
            IApplicationInstanceSelector instanceSelector,
            ISystemLog log,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, log, logFileOnlyLogger)
        {
            this.configurationEncryptionUpgrade = configurationEncryptionUpgrade;
            this.instanceSelector = instanceSelector;
            this.log = log;
        }

        protected override void Start()
        {
            base.Start();

            var instance = instanceSelector.Current;
            if (PlatformDetection.IsRunningOnWindows || !(instance.WritableConfiguration is XmlFileKeyValueStore))
            {
                log.Info("Configuration encryption keys only apply to Linux Tentacles with a configuration file; nothing to do.");
                return;
            }

            if (instance.KeyFilePath == null)
            {
                throw new ControlledFailureException($"The configuration `{instance.ConfigurationPath}` is not in the instance registry, so it has no encryption key of its own. "
                    + $"Register it with `create-instance --instance <name> --config {instance.ConfigurationPath}`, then run this command again.");
            }

            configurationEncryptionUpgrade.Value.Upgrade();
            log.Info($"The configuration is protected with the encryption key `{instance.KeyFilePath}`.");
        }
    }
}
