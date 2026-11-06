using System;
using System.Security.Cryptography;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Crypto
{
    // The file is named differently from the machinekey earlier versions generated so that a current key is always
    // freshly generated, owner-only from the start: a legacy key file was world-readable for as long as it existed, so it
    // is never adopted as the current key, only read to decrypt what earlier versions wrote. The format is
    // base64(key).base64(iv), the same as the legacy file so one reader serves both; the IV is only used to decrypt
    // values those versions wrote, which encrypted everything with that one IV.
    public class LinuxGeneratedMachineKey : ICryptoKeyNixSource
    {
        public const string KeyFileName = "config-encryption.key";

        // Only ever read, never written.
        public const string LegacyKeyFileName = "machinekey";

        public const string LegacyKeyFilePath = "/etc/octopus/" + LegacyKeyFileName;

        readonly ISystemLog log;
        readonly IOctopusFileSystem fileSystem;
        readonly bool createIfMissing;
        readonly string? ownerReferencePath;
        (byte[] Key, byte[] IV)? loaded;

        public LinuxGeneratedMachineKey(ISystemLog log, IOctopusFileSystem fileSystem, string keyFilePath, bool createIfMissing = true, string? ownerReferencePath = null)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            KeyFilePath = keyFilePath;
            this.createIfMissing = createIfMissing;
            this.ownerReferencePath = ownerReferencePath;
        }

        public string KeyFilePath { get; }

        public static string KeyFilePathFor(string configurationFile)
            => JoinLinuxPath(LinuxDirectoryOf(configurationFile), KeyFileName);

        // Evaluated on every call because it depends on the environment, which tests change.
        public static string LegacyKeyFilePathForThisHost
            => KubernetesSupportDetection.IsRunningAsKubernetesAgent
                ? JoinLinuxPath(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleHome)!, LegacyKeyFileName)
                : LegacyKeyFilePath;

        public (byte[] Key, byte[] IV) Load()
        {
            var existing = TryLoad();
            if (existing.HasValue)
            {
                return existing.Value;
            }

            if (!createIfMissing)
            {
                throw new InvalidOperationException($"There is no machine key file at `{KeyFilePath}`.");
            }

            Create(KeyFilePath);
            var key = LoadFromFile(KeyFilePath);
            loaded = key;
            return key;
        }

        public (byte[] Key, byte[] IV)? TryLoad()
        {
            if (loaded.HasValue)
            {
                return loaded;
            }

            if (!fileSystem.FileExists(KeyFilePath))
            {
                return null;
            }

            loaded = LoadFromFile(KeyFilePath);
            return loaded;
        }

        // Earlier versions created the key file with whatever the umask allowed. Only the agent calls this, as the user it
        // runs as: tightening from a command run as root (sudo tentacle show-thumbprint, say) could lock out a service user
        // that relies on reading a file it does not own.
        public void RestrictPermissionsToOwner()
        {
            var path = KeyFilePath;
            try
            {
                if (!fileSystem.FileExists(path))
                {
                    return;
                }
                if (fileSystem.RestrictFilePermissionsToOwner(path))
                {
                    log.Info($"Restricted the permissions on the machine key file `{path}` so that only its owner can read it.");
                }
            }
            catch (Exception e)
            {
                log.Verbose(e, $"Unable to restrict the permissions on the machine key file `{path}`. The key is still usable.");
            }
        }

        // The configuration has to move too: the agent restricts it to its owner when it starts (see
        // XmlFileKeyValueStore.RestrictKeyStorageToOwner), so after the service user changes, the new user could not
        // otherwise read a configuration the previous one owned. Must run as root.
        public static bool GiveKeyAndConfigurationToServiceUser(ISystemLog log, IOctopusFileSystem fileSystem, string configurationFile, string userName)
        {
            var key = new LinuxGeneratedMachineKey(log, fileSystem, KeyFilePathFor(configurationFile), createIfMissing: true, ownerReferencePath: configurationFile);
            try
            {
                key.Load();
            }
            catch (Exception e)
            {
                log.Warn(e, $"Unable to create the machine key file `{key.KeyFilePath}` for the service user {userName}.");
                return false;
            }

            var gaveKey = Give(key.KeyFilePath, "machine key file");
            var gaveConfiguration = !fileSystem.FileExists(configurationFile) || Give(configurationFile, "configuration file");
            return gaveKey && gaveConfiguration;

            bool Give(string path, string description)
            {
                if (fileSystem.TryChangeOwner(path, userName))
                {
                    log.Info($"The {description} `{path}` now belongs to {userName}, the user the service runs as.");
                    return true;
                }

                log.Warn($"Unable to give the {description} `{path}` to {userName}, the user the service runs as. "
                    + $"If Tentacle cannot read it when it starts, run `sudo chown {userName} {path}`.");
                return false;
            }
        }

        void Create(string path)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateIV();
            aes.GenerateKey();
            var raw = Convert.ToBase64String(aes.Key) + "." + Convert.ToBase64String(aes.IV);

            fileSystem.EnsureDirectoryExists(LinuxDirectoryOf(path));

            // Written complete under another name and moved into place without replacing anything, so two processes
            // generating a key at once (several instances starting after an upgrade, say) cannot each keep a different
            // one: the loser loads the winner's key from the file below.
            if (!fileSystem.TryCreateFileOwnerOnly(path, raw))
            {
                log.Verbose($"Another process created the machine key file `{path}` first; using its key.");
                return;
            }

            log.Info($"Generated a new machine key at `{path}`. Keep it with the configuration file it protects: the configuration cannot be decrypted without it.");

            if (ownerReferencePath != null && fileSystem.FileExists(ownerReferencePath) && fileSystem.TryChangeOwnerToMatch(path, ownerReferencePath))
            {
                log.Verbose($"Gave the machine key file `{path}` the same owner as `{ownerReferencePath}`.");
            }
        }

        (byte[] Key, byte[] IV) LoadFromFile(string path)
        {
            string contents;
            try
            {
                contents = fileSystem.ReadAllText(path);
            }
            catch (UnauthorizedAccessException e)
            {
                var user = Environment.UserName;
                throw new InvalidOperationException($"The machine key file `{path}` exists but the user Tentacle is running as ({user}) cannot read it. "
                    + $"It must be readable by that user, for example: `sudo chown {user} {path}`, or install the service with `tentacle service --install --username {user}` as root.", e);
            }

            try
            {
                var content = contents.Split('.');
                var key = Convert.FromBase64String(content[0]);
                var iv = Convert.FromBase64String(content[1]);
                if (key.Length == 0)
                {
                    throw new FormatException("The key is empty.");
                }
                return (key, iv);
            }
            catch (Exception ex) when (ex is FormatException || ex is IndexOutOfRangeException)
            {
                throw new InvalidOperationException($"Machine key file at `{path}` is corrupt and cannot be loaded. "
                    + "If this file previously contained a valid key to encrypt data, that data may no longer be retrievable. "
                    + $"Remove the file `{path}` and allow Octopus to regenerate the key.");
            }
        }

        // These are always Linux paths, whatever the host: this class only runs on Linux in production, but its unit
        // tests also run on Windows, where Path.Combine and Path.GetDirectoryName would switch to backslashes.
        static string JoinLinuxPath(string directory, string fileName)
            => directory.EndsWith("/", StringComparison.Ordinal) ? directory + fileName : directory + "/" + fileName;

        static string LinuxDirectoryOf(string path)
        {
            var lastSeparator = path.LastIndexOf('/');
            return lastSeparator <= 0 ? "/" : path.Substring(0, lastSeparator);
        }
    }
}
