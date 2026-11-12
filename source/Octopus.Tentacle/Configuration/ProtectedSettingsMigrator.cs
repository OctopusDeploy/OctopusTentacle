using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Configuration
{
    public interface IProtectedSettingsMigrator
    {
        void ReEncryptLegacyProtectedSettings();
    }

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
            // Only the file-backed store has a legacy scheme; the Kubernetes agent's ConfigMap store does not.
            if (!(instanceSelector.Current.WritableConfiguration is IReEncryptingKeyValueStore store))
            {
                return;
            }

            foreach (var name in ProtectedSettingNames)
            {
                try
                {
                    if (store.ReEncryptIfLegacy(name))
                    {
                        log.Info($"Re-encrypted the protected setting '{name}' with the configuration encryption key.");
                    }
                }
                catch (Exception e)
                {
                    log.Error(e, $"Unable to re-encrypt the protected setting '{name}' with the configuration encryption key, so it is still protected by the key earlier versions used. "
                        + "Tentacle will keep working and try again the next time it starts. "
                        + $"To finish the migration, run `sudo tentacle service --reconfigure --username {Environment.UserName}`, or make {ConfigurationDirectory()} writable by {Environment.UserName}.");
                }
            }

            store.RestrictKeyStorageToOwner();
        }

        string ConfigurationDirectory()
        {
            var path = instanceSelector.Current.ConfigurationPath;
            return path is null ? "the configuration directory" : Path.GetDirectoryName(path)!;
        }
    }
}
