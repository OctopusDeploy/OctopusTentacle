using System;
using System.Security.Cryptography;
using System.Text;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Crypto
{
    // The legacy machinekey file was world-readable, so it is only ever read to decrypt what earlier versions wrote and is
    // never adopted as the current key. Both files are base64(key).base64(iv); the IV is only used by the legacy scheme.
    public class LinuxGeneratedMachineKey : ICryptoKeyNixSource
    {
        public const string KeyFileName = "config-encryption.key";
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

        // Earlier versions created the key file with whatever the umask allowed.
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
                log.Warn(e, $"Unable to restrict the permissions on the machine key file `{path}`. The key is still usable.");
            }
        }

        // Both files are owner-only, so the service user must own them to read them. Must run as root.
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
                if (fileSystem.TryChangeOwner(path, userName, out var reason))
                {
                    log.Info($"The {description} `{path}` now belongs to {userName}, the user the service runs as.");
                    return true;
                }

                log.Warn($"Unable to give the {description} `{path}` to {userName}, the user the service runs as: {reason}. "
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

            // Several instances starting at once after an upgrade must all end up with the same key.
            if (!fileSystem.TryCreateFileOwnerOnly(path, Encoding.ASCII.GetBytes(raw), ownerReferencePath))
            {
                log.Verbose($"Another process created the machine key file `{path}` first; using its key.");
                return;
            }

            log.Info($"Generated a new machine key at `{path}`. Keep it with the configuration file it protects: the configuration cannot be decrypted without it.");
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

        // Always Linux paths, even when unit tests run on Windows.
        static string JoinLinuxPath(string directory, string fileName)
            => directory.EndsWith("/", StringComparison.Ordinal) ? directory + fileName : directory + "/" + fileName;

        static string LinuxDirectoryOf(string path)
        {
            var lastSeparator = path.LastIndexOf('/');
            return lastSeparator <= 0 ? "/" : path.Substring(0, lastSeparator);
        }
    }
}
