using System;
using System.Security.Cryptography;

namespace Octopus.Shared.Security.MasterKey
{
    public static class MasterKeyEncryption
    {
        const int KeySizeBits = 128, BlockSizeBits = 128;

        public static byte[] GenerateKey()
        {
            var key = new byte[KeySizeBits / 8];
            using (var provider = new RNGCryptoServiceProvider())
                provider.GetBytes(key);
            return key;
        }

        public static SymmetricAlgorithm CreateAlgorithm(byte[] key)
        {
            var algorithm = new AesCryptoServiceProvider
            {
                Padding = PaddingMode.PKCS7, 
                KeySize = KeySizeBits, 
                Key = key, 
                BlockSize = BlockSizeBits, 
                Mode = CipherMode.CBC,
                IV = new byte[BlockSizeBits/8]
            };

            return algorithm;
        }

        public static Type Algorithm
        {
            get
            {
                return typeof(AesCryptoServiceProvider);
            }
        }
    }
}
