using System;
using System.IO;
using System.Security.Cryptography;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public class LinuxGeneratedMachineKey: ICryptoKeyNixSource
    {
        readonly ISystemLog log;
        readonly IOctopusFileSystem fileSystem;
        static string FileName = "/etc/octopus/machinekey";

        public LinuxGeneratedMachineKey(ISystemLog log, IOctopusFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        void Generate()
        {
            log.Verbose("Machine key file does not yet exist. Generating key file that will be used to encrypt data on this machine");
            var d = new RijndaelManaged();
            d.GenerateIV();
            d.GenerateKey();
            var raw = Convert.ToBase64String(d.Key) + "." + Convert.ToBase64String(d.IV);

            if (!fileSystem.FileExists(Path.GetDirectoryName(FileName)!))
                fileSystem.CreateDirectory(Path.GetDirectoryName(FileName)!);

            fileSystem.WriteAllText(FileName, raw);
        }

        (byte[] Key, byte[] IV) LoadFromFile()
        {
            try
            {
                var content = fileSystem.ReadAllText(FileName).Split('.');
                var key = Convert.FromBase64String(content[0]);
                var iv = Convert.FromBase64String(content[1]);
                return (key, iv);
            }
            catch (Exception ex) when (ex is FormatException || ex is IndexOutOfRangeException)
            {
                throw new InvalidOperationException($"Machine key file at `{FileName}` is corrupt and cannot be loaded. "
                    + $"If this file previously contained a valid key to encrypt data, that data may no longer be retrievable."
                    + $"Remove the file `{FileName}` and allow Octopus to regenerate the key.");
            }
        }

        public (byte[] Key, byte[] IV) Load()
        {
            if (!fileSystem.FileExists(FileName))
                Generate();
            return LoadFromFile();
        }
    }
}