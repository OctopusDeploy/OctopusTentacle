using System;
using System.Collections.Generic;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Configuration
{
    public interface IProtectedSettingsMigrator
    {
        /// <summary>
        /// Re-encrypts every <see cref="ProtectionLevel.MachineKey"/> setting in the current instance's configuration
        /// that was written by a superseded encryption scheme. Safe to call on every start: it does nothing once there
        /// is nothing left to migrate, and a setting that cannot be rewritten (read-only configuration, say) is logged
        /// and left as it is, still readable through the old scheme.
        /// </summary>
        void ReEncryptLegacyProtectedSettings();
    }

    /// <summary>
    /// Retires the encryption key earlier Linux versions derived from <c>/etc/machine-id</c>: that key is shared by
    /// every container started from the same image and is world-readable on any host, so values written with it are
    /// rewritten with the key Tentacle now generates per machine. See <see cref="Crypto.LinuxMachineKeyEncryptor"/>.
    /// </summary>
    public class ProtectedSettingsMigrator : IProtectedSettingsMigrator
    {
        /// <summary>
        /// Every setting that Tentacle stores in its configuration file with <see cref="ProtectionLevel.MachineKey"/>.
        /// </summary>
        public static readonly IReadOnlyList<string> ProtectedSettingNames = new[]
        {
            TentacleConfiguration.CertificateSettingName,
            ProxyConfiguration.ProxyPasswordSettingName,
            PollingProxyConfiguration.ProxyPasswordSettingName
        };

        readonly IApplicationInstanceSelector instanceSelector;
        readonly ISystemLog log;

        public ProtectedSettingsMigrator(IApplicationInstanceSelector instanceSelector, ISystemLog log)
        {
            this.instanceSelector = instanceSelector;
            this.log = log;
        }

        public void ReEncryptLegacyProtectedSettings()
        {
            // The Kubernetes agent keeps its configuration in a ConfigMap with its own per-agent key, and other
            // stores have no notion of a legacy scheme; only the file-backed store has anything to migrate.
            if (!(instanceSelector.Current.WritableConfiguration is IReEncryptingKeyValueStore store))
                return;

            foreach (var name in ProtectedSettingNames)
            {
                try
                {
                    if (store.ReEncryptIfLegacy(name))
                        log.Info($"Re-encrypted the protected setting '{name}' with the machine key generated for this machine.");
                }
                catch (Exception e)
                {
                    log.Warn(e, $"Unable to re-encrypt the protected setting '{name}' with the machine key generated for this machine. "
                        + "The existing value is still readable and Tentacle will try again the next time it starts. "
                        + "This is expected when the configuration directory is read-only.");
                }
            }
        }
    }
}
