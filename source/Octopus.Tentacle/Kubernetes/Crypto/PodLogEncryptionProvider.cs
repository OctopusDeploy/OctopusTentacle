using System;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.Encoders;

namespace Octopus.Tentacle.Kubernetes.Crypto
{
    public interface IPodLogEncryptionProvider
    {
        string Decrypt(string encryptedLogMessage);
        string Encrypt(string plainText, byte[]? nonce = null);
    }
    
    public class PodLogEncryptionProvider : IPodLogEncryptionProvider
    {
        readonly KeyParameter keyParameter;
        readonly GcmBlockCipher cipher;
        readonly int macSize;
        const int NonceLength = 12;

        private PodLogEncryptionProvider(byte[] keyBytes)
        {
            keyParameter = new KeyParameter(keyBytes);
            cipher = new GcmBlockCipher(new AesEngine());
            macSize = 8 * cipher.GetBlockSize();
        }

        public static IPodLogEncryptionProvider Create(byte[] keyBytes) => new PodLogEncryptionProvider(keyBytes);

        public string Decrypt(string encryptedLogMessage)
        {
            var allEncryptedBytes = Hex.Decode(encryptedLogMessage).AsSpan();

            var nonceSpan = allEncryptedBytes.Slice(0, NonceLength);
            var logMessageBytes = allEncryptedBytes.Slice(NonceLength);

            cipher.Init(false, new AeadParameters(keyParameter, macSize, nonceSpan.ToArray()));

            var outputSize = cipher.GetOutputSize(logMessageBytes.Length);
            var plainTextData = new byte[outputSize];

            var result = cipher.ProcessBytes(logMessageBytes.ToArray(), 0, logMessageBytes.Length, plainTextData, 0);
            cipher.DoFinal(plainTextData, result);

            return Encoding.UTF8.GetString(plainTextData);
        }

        public string Encrypt(string plainText, byte[]? nonce = null)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            //if no nonce is provided, generate one
            nonce ??= GenerateNonce();

            cipher.Init(true, new AeadParameters(keyParameter, macSize, nonce, null));

            var cipherText = new byte[cipher.GetOutputSize(plainTextBytes.Length)];
            var len = cipher.ProcessBytes(plainTextBytes, 0, plainTextBytes.Length, cipherText, 0);
            cipher.DoFinal(cipherText, len);

            var allBytes = new byte[nonce.Length + cipherText.Length];
            Array.Copy(nonce,0,allBytes, 0, nonce.Length);
            Array.Copy(cipherText, 0, allBytes, nonce.Length, cipherText.Length);

            return Hex.ToHexString(allBytes);
        }
        
#if NETFRAMEWORK
        static readonly RNGCryptoServiceProvider RandomCryptoServiceProvider = new RNGCryptoServiceProvider();
#endif
        static byte[] GenerateNonce()
        {
#if NETFRAMEWORK
            var buffer = new byte[NonceLength];
            RandomCryptoServiceProvider.GetBytes(buffer);
            return buffer;
#else
            return RandomNumberGenerator.GetBytes(NonceLength);
#endif
        }
    }
}