using System;
using System.Security.Cryptography;
using System.Text;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Crypto
{
    // The per-instance AES-256 key that protects a configuration file on Linux, stored as base64(key).
    public class ConfigurationEncryptionKey
    {
        public const string KeyFileName = "config-encryption.key";

        // Kept apart from /etc/octopus so that a copy of the configuration does not also carry the key that decrypts it.
        public const string DefaultKeysDirectory = "/var/octopus/tentacle/keys";

        const int KeySizeInBytes = 32;

        const string RecoveryWithoutBackup = "If there is no backup, the Tentacle's certificate is lost and it needs a new one: "
            + "run `replace-certificate` with an API key. "
            + "This creates a new key and certificate, and updates every deployment target and worker registered with the old certificate to trust it.";

        static string KeysDirectory
            => Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleConfigurationKeysDirectory) is { Length: > 0 } directory ? directory : DefaultKeysDirectory;

        readonly ISystemLog log;
        readonly IOctopusFileSystem fileSystem;
        byte[]? loaded;

        // A null path means the configuration has no key, for example because it is not registered as an instance.
        public ConfigurationEncryptionKey(ISystemLog log, IOctopusFileSystem fileSystem, string? keyFilePath)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            KeyFilePath = keyFilePath;
        }

        public string? KeyFilePath { get; }

        public string? KeyDirectory => KeyFilePath == null ? null : LinuxDirectoryOf(KeyFilePath);

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

        public byte[] Load()
            => TryLoad() ?? throw new InvalidOperationException(KeyFilePath == null
                ? "The configuration encryption key for this configuration is not available. Register the configuration with `create-instance --instance <name>`."
                : $"The configuration encryption key file `{KeyFilePath}` does not exist, so the values it protected cannot be decrypted. Restore it from a backup. " + RecoveryWithoutBackup);

        public byte[]? TryLoad()
        {
            if (loaded != null)
            {
                return loaded;
            }

            if (KeyFilePath == null || !fileSystem.FileExists(KeyFilePath))
            {
                return null;
            }

            loaded = LoadFromFile(KeyFilePath);
            return loaded;
        }

        // Only the configuration upgrade creates the key, so that it can then give the key to the configuration's owner.
        public void CreateIfMissing()
        {
            var path = KeyFilePath ?? throw new InvalidOperationException("There is no key file path to create the configuration encryption key at.");
            if (fileSystem.FileExists(path))
            {
                return;
            }

            var raw = Convert.ToBase64String(RandomKey());

            fileSystem.EnsureDirectoryExists(KeyDirectory!);

            // Several instances starting at once after an upgrade must all end up with the same key.
            if (!fileSystem.AtomicCreateFileWithUserReadWriteAccessOnly(path, Encoding.ASCII.GetBytes(raw)))
            {
                log.Verbose($"Another process created the configuration encryption key `{path}` first; using its key.");
                return;
            }

            log.Info($"Generated a new configuration encryption key at `{path}`. Back it up with the configuration file it protects: the configuration cannot be decrypted without it.");
        }

        static byte[] RandomKey()
        {
            var key = new byte[KeySizeInBytes];
            using var random = RandomNumberGenerator.Create();
            random.GetBytes(key);
            return key;
        }

        byte[] LoadFromFile(string path)
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
                throw new InvalidOperationException($"The configuration encryption key file `{path}` exists but the user Tentacle is running as ({user}) cannot read it. "
                    + $"It must be owned by and readable only by that user, for example: `sudo chown -R {user} {directory} && sudo chmod 700 {directory} && sudo chmod 600 {path}`.", e);
            }

            try
            {
                var key = Convert.FromBase64String(contents.Trim());
                if (key.Length == KeySizeInBytes)
                {
                    return key;
                }
            }
            catch (FormatException)
            {
            }

            throw new InvalidOperationException($"The configuration encryption key file `{path}` is corrupt: it must hold a base64-encoded {KeySizeInBytes * 8}-bit key. "
                + "Restore it from a backup; values encrypted with a lost key cannot be recovered. " + RecoveryWithoutBackup);
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
