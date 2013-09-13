using System;
using System.Security.Cryptography;

namespace Octopus.Shared.Security.MasterKey
{
    public class MasterKeyEncryption
    {
        const int KeySizeBits = 128, BlockSizeBits = 128;

        public static byte[] GenerateKey()
        {
            var key = new byte[KeySizeBits / 8];
            using (var provider = new RNGCryptoServiceProvider())
                provider.GetBytes(key);
            return key;
        }

        public static SymmetricAlgorithm CreateAlgorithm(byte[] key, bool generateSalt = false)
        { 
            // If generateSalt is true, we'll let the algorithm generate the salt itself,
            // since that will use the underlying provider.

            var algorithm = new AesCryptoServiceProvider
            {
                Padding = PaddingMode.PKCS7, 
                KeySize = KeySizeBits, 
                Key = key, 
                BlockSize = BlockSizeBits, 
                Mode = CipherMode.CBC,
            };

            if (!generateSalt)
                algorithm.IV = new byte[BlockSizeBits / 8];

            return algorithm;
        }

        public static Type AlgorithmType
        {
            get
            {
                return typeof(AesCryptoServiceProvider);
            }
        }
    }
}
