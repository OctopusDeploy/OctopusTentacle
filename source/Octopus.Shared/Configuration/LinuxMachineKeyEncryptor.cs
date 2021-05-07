using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Octopus.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class LinuxMachineKeyEncryptor : IMachineKeyEncryptor
    {
        readonly IEnumerable<ICryptoKeyNixSource> keySources;

        public LinuxMachineKeyEncryptor(IEnumerable<ICryptoKeyNixSource> keySources)
        {
            this.keySources = keySources;
        }
        
        string IterateKeySourcesUntilCryptoSuccess(Func<(byte[] Key, byte[] IV), string> cipherOp)
        {
            var ex = new List<Exception>();
            foreach (var source in keySources)
            {
                try
                {
                    return cipherOp(source.Load());
                }
                catch (Exception e)
                {
                    ex.Add(e);
                }
            }

            throw new AggregateException(ex);
        }

        public string Encrypt(string raw)
        {
            return IterateKeySourcesUntilCryptoSuccess(cipherKeys =>
            {
                var (key, iv) = cipherKeys;
                using (var rijandel = new RijndaelManaged())
                using (var enc = rijandel.CreateEncryptor(key, iv))
                {
                    var inBlock = Encoding.UTF8.GetBytes(raw);
                    var trans = enc.TransformFinalBlock(inBlock, 0, inBlock.Length);
                    return Convert.ToBase64String(trans);
                }
            });
        }

        public string Decrypt(string encrypted)
        {
            return IterateKeySourcesUntilCryptoSuccess(cipherKeys =>
            {
                var (key, iv) = cipherKeys;
                using (var rijandel = new RijndaelManaged())
                using (var dec = rijandel.CreateDecryptor(key, iv))
                {
                    var fromBase = Convert.FromBase64String(encrypted);
                    var asd = dec.TransformFinalBlock(fromBase, 0, fromBase.Length);
                    return Encoding.UTF8.GetString(asd);
                }
            });
        }

        

        public interface ICryptoKeyNixSource
        {
            (byte[] Key, byte[] IV) Load();
        }
        
        internal class LinuxMachineIdKey: ICryptoKeyNixSource
        {
            static string FileName = "/etc/machine-id";
            static byte[] iv = "743677397A244326".Select(Convert.ToByte).ToArray();
            
            public LinuxMachineIdKey(ISystemLog log){}

            static byte[] GetMachindId()
            {
                var lines = File.ReadLines(FileName).First();
                return lines.Select(Convert.ToByte).ToArray();
            }

            public (byte[] Key, byte[] IV) Load()
            {
                return (GetMachindId(), iv);
            }
        }

        internal class LinuxGeneratedMachineKey: ICryptoKeyNixSource
        {
            readonly ISystemLog log;
            internal static string FileName = "/etc/octopus/machinekey";

            public LinuxGeneratedMachineKey(ISystemLog log)
            {
                this.log = log;
            }

            void Generate()
            {
                log.Verbose("Machine key file does not yet exist. Generating key file that will be used to encrypt data on this machine");
                var d = new RijndaelManaged();
                d.GenerateIV();
                d.GenerateKey();
                var raw = Convert.ToBase64String(d.Key) + "." + Convert.ToBase64String(d.IV);

                if (!Directory.Exists(Path.GetDirectoryName(FileName)))
                    Directory.CreateDirectory(Path.GetDirectoryName(FileName));

                File.WriteAllText(FileName, raw);
            }

            static (byte[] Key, byte[] IV) LoadFromFile()
            {
                try
                {
                    var content = File.ReadAllText(FileName).Split('.');
                    var key = Convert.FromBase64String(content[0]);
                    var iv = Convert.FromBase64String(content[1]);
                    return (key, iv);
                }
                catch (Exception ex) when (ex is FormatException || ex is IndexOutOfRangeException)
                {
                    throw new InvalidOperationException($"Machine key file at `{FileName}` is corrupt and cannot be loaded");
                }
            }

            public (byte[] Key, byte[] IV) Load()
            {
                if (!File.Exists(FileName))
                    Generate();
                return LoadFromFile();
            }
        }
    }
}