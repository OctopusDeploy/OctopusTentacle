using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration
{
    public interface ITentacleConfigurationUpgrade
    {
        // Creates the configuration encryption key if it is missing, and gives it to the configuration's owner when running as root.
        void PrepareKey();

        // Prepares the key, then re-encrypts protected settings written with the keys earlier versions used.
        void Upgrade();

        // As PrepareKey and Upgrade, but a failure is logged as a warning and Tentacle carries on with the keys earlier versions used.
        void TryPrepareKey();
        void TryUpgrade();
    }

    // Until Upgrade succeeds, Tentacle keeps working on the keys earlier versions used.
    public class TentacleConfigurationUpgrade : ITentacleConfigurationUpgrade
    {
        public static readonly IReadOnlyList<string> ProtectedSettingNames = new[]
        {
            TentacleConfiguration.CertificateSettingName,
            ProxyConfiguration.ProxyPasswordSettingName,
            PollingProxyConfiguration.ProxyPasswordSettingName
        };

        const string StillOnLegacyKeys = "so the configuration is still protected by the key earlier versions used. Tentacle will keep working and try again the next time it starts.";

        readonly IApplicationInstanceSelector instanceSelector;
        readonly IOctopusFileSystem fileSystem;
        readonly ISystemLog log;

        public TentacleConfigurationUpgrade(IApplicationInstanceSelector instanceSelector, IOctopusFileSystem fileSystem, ISystemLog log)
        {
            this.instanceSelector = instanceSelector;
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public void PrepareKey()
            => PrepareInstanceKey();

        public void TryPrepareKey()
            => WarnOnFailure(PrepareKey, "Unable to prepare the configuration encryption key");

        public void TryUpgrade()
            => WarnOnFailure(Upgrade, "Unable to move the configuration to its own encryption key");

        void WarnOnFailure(Action action, string failure)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                log.Warn(e, $"{failure}, {StillOnLegacyKeys}");
            }
        }

        public void Upgrade()
        {
            var prepared = PrepareInstanceKey();
            if (prepared == null)
            {
                return;
            }

            var (key, store) = prepared.Value;
            key.Load();
            foreach (var name in ProtectedSettingNames)
            {
                if (store.ReEncryptIfLegacy(name))
                {
                    log.Info($"Re-encrypted the protected setting '{name}' with the configuration encryption key.");
                }
            }
        }

        // Null when the instance has no key to prepare.
        (ConfigurationEncryptionKey Key, XmlFileKeyValueStore Store)? PrepareInstanceKey()
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                return null;
            }

            // Only the file-backed store has a legacy scheme; the Kubernetes agent's ConfigMap store does not.
            var instance = instanceSelector.Current;
            if (!(instance.WritableConfiguration is XmlFileKeyValueStore store) || instance.ConfigurationPath == null || !File.Exists(instance.ConfigurationPath))
            {
                return null;
            }

            if (instance.KeyFilePath == null)
            {
                log.Warn($"The instance using `{instance.ConfigurationPath}` is not in the instance registry, so its configuration stays protected by the key earlier versions used. "
                    + "Register it with `create-instance --instance <name> --config <path>` to upgrade it.");
                return null;
            }

            var key = new ConfigurationEncryptionKey(log, fileSystem, instance.KeyFilePath);
            key.CreateIfMissing();

#if NET
            var configurationOwner = UnixFile.OwnerOf(instance.ConfigurationPath);
            UnixFile.SetOwnerIfRunningAsRoot(configurationOwner, key.KeyDirectory!);
            UnixFile.SetOwnerIfRunningAsRoot(configurationOwner, key.KeyFilePath!);
#endif

            return (key, store);
        }
    }
}
