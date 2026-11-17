using System;
using System.IO;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Crypto
{
    // The key file earlier versions generated on first use, as base64(key).base64(iv). It is only read, to decrypt what they wrote.
    public class LinuxGeneratedMachineKey : ICryptoKeyNixSource
    {
        public const string LegacyKeyFileName = "machinekey";

        public const string LegacyKeyFilePath = "/etc/octopus/" + LegacyKeyFileName;

        readonly IOctopusFileSystem fileSystem;

        public LinuxGeneratedMachineKey(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public static string LegacyKeyFilePathForThisHost
            => KubernetesSupportDetection.IsRunningAsKubernetesAgent
                //if we are running in K8S, the machine key is in the home directory, which is likely on a network drive
                ? Path.Combine(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleHome)!, LegacyKeyFileName)
                : LegacyKeyFilePath;

        public (byte[] Key, byte[] IV) Load()
        {
            var path = LegacyKeyFilePathForThisHost;
            if (!fileSystem.FileExists(path))
            {
                throw new InvalidOperationException($"There is no machine key file at `{path}`.");
            }

            try
            {
                var content = fileSystem.ReadAllText(path).Split('.');
                return (Convert.FromBase64String(content[0]), Convert.FromBase64String(content[1]));
            }
            catch (Exception ex) when (ex is FormatException || ex is IndexOutOfRangeException)
            {
                throw new InvalidOperationException($"Machine key file at `{path}` is corrupt and cannot be loaded. "
                    + "If this file previously contained a valid key to encrypt data, that data may no longer be retrievable. "
                    + $"Remove the file `{path}` and allow Octopus to regenerate the key.");
            }
        }
    }
}
