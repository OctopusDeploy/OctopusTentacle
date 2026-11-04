using System;
using System.Collections.Generic;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Configuration
{
    public interface IProtectedSettingsMigrator
    {
        void ReEncryptLegacyProtectedSettings();
    }

    // Retires the key earlier Linux versions derived from /etc/machine-id: it is shared by every container started from
    // the same image and is world-readable on any host.
    public class ProtectedSettingsMigrator : IProtectedSettingsMigrator
    {
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
                        + "This is expected when the configuration directory is read-only, or is not writable by the user Tentacle runs as.");
                }
            }

            // Here rather than wherever the key is loaded: the agent runs as the service user, so tightening a key file
            // it owns can never lock out the process that needs to read it.
            store.RestrictKeyStorageToOwner();
        }
    }
}
