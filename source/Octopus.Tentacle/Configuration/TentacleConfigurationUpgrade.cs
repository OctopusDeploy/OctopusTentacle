using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration
{
    public interface ITentacleConfigurationUpgrade
    {
        // Creates the configuration encryption key if it is missing, and gives it to the configuration's owner when running as root.
        void PrepareKey();

        // Prepares the key, then re-encrypts protected settings written with the keys earlier versions used.
        void Run();
    }

    // Until Run succeeds, Tentacle keeps working on the keys earlier versions used.
    public class TentacleConfigurationUpgrade : ITentacleConfigurationUpgrade
    {
        public static readonly IReadOnlyList<string> ProtectedSettingNames = new[]
        {
            TentacleConfiguration.CertificateSettingName,
            ProxyConfiguration.ProxyPasswordSettingName,
            PollingProxyConfiguration.ProxyPasswordSettingName
        };

        public const string StillOnLegacyKeys = "so the configuration is still protected by the key earlier versions used. Tentacle will keep working and try again the next time it starts.";

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

        public void Run()
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
            var configurationOwner = UnixOwnership.OwnerOf(instance.ConfigurationPath);
            UnixOwnership.GiveToIfRoot(configurationOwner, key.KeyDirectory!);
            UnixOwnership.GiveToIfRoot(configurationOwner, key.KeyFilePath!);
#endif

            return (key, store);
        }

#if NET
        static class UnixOwnership
        {
            const uint UnchangedGroup = uint.MaxValue;

            public static uint OwnerOf(string path)
            {
                return SystemNative_Stat(path, out var status) == 0
                    ? status.Uid
                    : throw new IOException($"Unable to find the owner of `{path}`: {Marshal.GetLastPInvokeErrorMessage()}");
            }

            public static void GiveToIfRoot(uint owner, string path)
            {
                if (geteuid() != 0 || OwnerOf(path) == owner)
                {
                    return;
                }

                if (lchown(path, owner, UnchangedGroup) != 0)
                {
                    throw new IOException($"Unable to change the owner of `{path}`: {Marshal.GetLastPInvokeErrorMessage()}");
                }
            }

            [DllImport("libc")]
            static extern uint geteuid();

            [DllImport("libc", SetLastError = true)]
            static extern int lchown(string path, uint owner, uint group);

            // The .NET runtime's own stat shim: one struct layout on every libc and architecture.
            [DllImport("libSystem.Native", SetLastError = true)]
            static extern int SystemNative_Stat(string path, out FileStatus output);

            [StructLayout(LayoutKind.Sequential)]
            struct FileStatus
            {
                public int Flags;
                public int Mode;
                public uint Uid;
                public uint Gid;
                public long Size;
                public long ATime;
                public long ATimeNsec;
                public long MTime;
                public long MTimeNsec;
                public long CTime;
                public long CTimeNsec;
                public long BirthTime;
                public long BirthTimeNsec;
                public long Dev;
                public long RDev;
                public long Ino;
                public uint UserFlags;
            }
        }
#endif
    }
}
