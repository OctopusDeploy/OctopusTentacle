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

        // Kept apart from /etc/octopus so that a copy of the configuration does not also carry the key that decrypts it.
        public const string DefaultKeysDirectory = "/var/octopus/tentacle/keys";

        static string KeysDirectory
            => Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleConfigurationKeysDirectory) is { Length: > 0 } directory ? directory : DefaultKeysDirectory;

        readonly ISystemLog log;
        readonly IOctopusFileSystem fileSystem;
        (byte[] Key, byte[] IV)? loaded;

        public LinuxGeneratedMachineKey(ISystemLog log, IOctopusFileSystem fileSystem, string keyFilePath)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            KeyFilePath = keyFilePath;
        }

        public string KeyFilePath { get; }

        public string KeyDirectory => LinuxDirectoryOf(KeyFilePath);

        public static string KeyFilePathFor(string instanceName)
        {
            if (string.IsNullOrWhiteSpace(instanceName)
                || instanceName.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0
                || instanceName == "."
                || instanceName == "..")
            {
                throw new ArgumentException($"The instance name `{instanceName}` cannot be used as a key directory name.", nameof(instanceName));
            }

            return JoinLinuxPath(JoinLinuxPath(KeysDirectory, instanceName), KeyFileName);
        }

        public static string LegacyKeyFilePathForThisHost
            => KubernetesSupportDetection.IsRunningAsKubernetesAgent
                ? JoinLinuxPath(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleHome)!, LegacyKeyFileName)
                : LegacyKeyFilePath;

        public (byte[] Key, byte[] IV) Load()
            => TryLoad() ?? throw new InvalidOperationException($"There is no machine key file at `{KeyFilePath}`.");

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

        // Only the configuration upgrade creates the key, so that it can then give the key to the configuration's owner.
        public void CreateIfMissing()
        {
            if (fileSystem.FileExists(KeyFilePath))
            {
                return;
            }

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.GenerateIV();
            aes.GenerateKey();
            var raw = Convert.ToBase64String(aes.Key) + "." + Convert.ToBase64String(aes.IV);

            fileSystem.EnsureDirectoryExists(KeyDirectory);

            // Several instances starting at once after an upgrade must all end up with the same key.
            if (!fileSystem.AtomicCreateFileWithUserReadWriteAccessOnly(KeyFilePath, Encoding.ASCII.GetBytes(raw)))
            {
                log.Verbose($"Another process created the machine key file `{KeyFilePath}` first; using its key.");
                return;
            }

            log.Info($"Generated a new machine key at `{KeyFilePath}`. Back it up with the configuration file it protects: the configuration cannot be decrypted without it.");
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
                var directory = LinuxDirectoryOf(path);
                throw new InvalidOperationException($"The machine key file `{path}` exists but the user Tentacle is running as ({user}) cannot read it. "
                    + $"It must be owned by and readable only by that user, for example: `sudo chown -R {user} {directory} && sudo chmod 700 {directory} && sudo chmod 600 {path}`.", e);
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
