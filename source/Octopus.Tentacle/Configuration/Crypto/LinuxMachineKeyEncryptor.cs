using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public class LinuxMachineKeyEncryptor : IMachineKeyEncryptor
    {
        private readonly ISystemLog log;
        private readonly IEnumerable<ICryptoKeyNixSource> keySources;

        public LinuxMachineKeyEncryptor(ISystemLog log, IEnumerable<ICryptoKeyNixSource> keySources)
        {
            this.log = log;
            this.keySources = keySources;
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

        private string IterateKeySourcesUntilCryptoSuccess(Func<(byte[] Key, byte[] IV), string> cipherOp)
        {
            var ex = new List<Exception>();
            foreach (var source in keySources)
                try
                {
                    return cipherOp(source.Load());
                }
                catch (Exception e)
                {
                    log.Verbose(e.Message);
                    ex.Add(e);
                }

            throw new AggregateException(ex);
        }
    }
}