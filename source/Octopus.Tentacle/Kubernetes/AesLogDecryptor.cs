using System;
using System.IO;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Octopus.Tentacle.Kubernetes
{
    public static class AesLogDecryptor
    {
        public static string DecryptLogMessage(string encryptedLogMessage, byte[] aesKey)
        {
            var allEncryptedBytes = FromHex(encryptedLogMessage);

            var nonceSpan = allEncryptedBytes.Slice(0,12);
            var logMessageBytes = allEncryptedBytes.Slice(12);
            
            var cipher = new GcmBlockCipher(new AesEngine());
            var macSize = 8 * cipher.GetBlockSize();
            cipher.Init(false, new AeadParameters(new KeyParameter(aesKey), macSize, nonceSpan.ToArray()));

            var outputSize = cipher.GetOutputSize(logMessageBytes.Length);
            var plainTextData = new byte[outputSize];

            var result = cipher.ProcessBytes(logMessageBytes.ToArray(), 0, logMessageBytes.Length, plainTextData, 0);
            cipher.DoFinal(plainTextData, result);

            return Encoding.UTF8.GetString(plainTextData);
        }

        static Span<byte> FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            var raw = new byte[hex.Length / 2];
            for (var i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return raw;
        }
    }
}