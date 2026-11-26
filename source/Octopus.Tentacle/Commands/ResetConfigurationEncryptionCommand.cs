using System;
using System.Linq;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class ResetConfigurationEncryptionCommand : AbstractStandardCommand
    {
        readonly Lazy<IConfigurationEncryptionUpgrade> configurationEncryptionUpgrade;
        readonly IApplicationInstanceSelector instanceSelector;
        readonly IOctopusFileSystem fileSystem;
        readonly ISystemLog log;

        public ResetConfigurationEncryptionCommand(
            Lazy<IConfigurationEncryptionUpgrade> configurationEncryptionUpgrade,
            IApplicationInstanceSelector instanceSelector,
            IOctopusFileSystem fileSystem,
            ISystemLog log,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(instanceSelector, log, logFileOnlyLogger)
        {
            this.configurationEncryptionUpgrade = configurationEncryptionUpgrade;
            this.instanceSelector = instanceSelector;
            this.fileSystem = fileSystem;
            this.log = log;
        }

        protected override void Start()
        {
            base.Start();

            var instance = instanceSelector.Current;
            if (PlatformDetection.IsRunningOnWindows || !(instance.WritableConfiguration is XmlFileKeyValueStore store))
            {
                log.Info("Configuration encryption keys only apply to Linux Tentacles with a configuration file; nothing to do.");
                return;
            }

            if (instance.KeyFilePath == null)
            {
                throw new ControlledFailureException($"The configuration `{instance.ConfigurationPath}` is not in the instance registry, so it has no encryption key of its own. "
                    + $"Register it with `create-instance --instance <name> --config {instance.ConfigurationPath}`, then run this command again.");
            }

            // Only a key that is genuinely missing may be replaced.
            try
            {
                if (new ConfigurationEncryptionKey(log, fileSystem, instance.KeyFilePath).TryLoad() != null)
                {
                    throw new ControlledFailureException($"The configuration encryption key `{instance.KeyFilePath}` exists, so there is nothing to reset.");
                }
            }
            catch (ConfigurationEncryptionKeyException e)
            {
                throw new ControlledFailureException($"{e.Message} If it is corrupt and there is no backup, move it aside and run this command again.");
            }

            var lost = ConfigurationEncryptionUpgrade.ProtectedSettingNames.Where(store.IsEncryptedWithCurrentKey).ToArray();
            if (lost.Length == 0)
            {
                throw new ControlledFailureException("No setting is encrypted with the configuration encryption key, so it is not needed yet. Run `upgrade-configuration-encryption` instead.");
            }

            var oldThumbprint = store.Get<string>(TentacleConfiguration.CertificateThumbprintSettingName);
            foreach (var name in lost)
            {
                store.Remove(name);
            }

            store.Save();
            log.Info($"Removed {string.Join(", ", lost)}, which cannot be decrypted without the lost key.");

            configurationEncryptionUpgrade.Value.PrepareKey();

            log.Info($"The old certificate thumbprint was {oldThumbprint}. "
                + "If you use a proxy password, re-enter it with `proxy` or `polling-proxy`. "
                + "Then run `replace-certificate` to give this Tentacle a new certificate and update Octopus Server to trust it. "
                + $"If the Tentacle has already generated a new certificate, run `replace-certificate --apply-current-cert-to-tentacles-with-thumbprint {oldThumbprint}` instead.");
        }
    }
}
