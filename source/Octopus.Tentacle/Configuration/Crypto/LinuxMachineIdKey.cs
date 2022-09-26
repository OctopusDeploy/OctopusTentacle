using System;
using System.Linq;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Crypto
{
    /// <summary>
    /// Uses the linux provided machine-unique code as described by https://www.commandlinux.com/man-page/man5/machine-id.5.html
    /// In some cases this file may not yet be populated, in which case we will either
    /// expect to fallback to the "old" linux mechanism <see cref="LinuxGeneratedMachineKey" />
    /// or if that fails (possibly for permissions issues) require the use to generate the id manually as per the docs.
    /// Note that it is expected that this will only work for linux machines while other Unix-like tentacles will have to still fall back
    /// to <see cref="LinuxGeneratedMachineKey" />.
    /// </summary>
    internal class LinuxMachineIdKey : ICryptoKeyNixSource
    {
        public static readonly string FileName = "/etc/machine-id";

        // Randomly generated IV, not required to be treated as sensitive
        private static readonly byte[] Iv = "743677397A244326".Select(Convert.ToByte).ToArray();

        private readonly IOctopusFileSystem fileSystem;

        public LinuxMachineIdKey(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public (byte[] Key, byte[] IV) Load()
        {
            return (GetMachineId(), Iv);
        }

        private byte[] GetMachineId()
        {
            if (!fileSystem.FileExists(FileName))
                throw new InvalidOperationException($"Unable to locate machineId file at {FileName}."
                    + "Please refer to http://g.octopushq.com/LinuxMachineId for the machine-id linux man page.");
            var machineId = fileSystem.ReadAllLines(FileName).FirstOrDefault();

            if (string.IsNullOrEmpty(machineId) || machineId.Length != 32)
                throw new InvalidOperationException($"machine-id contents at {FileName} is either empty or not 256 bits "
                    + "so is un-unable to be used for the encryption key."
                    + "Please refer to http://g.octopushq.com/LinuxMachineId for the machine-id linux man page.");
            return machineId.Select(Convert.ToByte).ToArray();
        }
    }
}