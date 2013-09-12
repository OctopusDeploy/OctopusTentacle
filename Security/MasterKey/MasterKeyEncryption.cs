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
            var algorithm = CryptoConfig.AllowOnlyFipsAlgorithms
                    ? (Aes)new AesCryptoServiceProvider()
                    : new AesManaged();

            algorithm.Padding = PaddingMode.PKCS7;
            algorithm.KeySize = KeySizeBits;
            algorithm.Key = key;
            algorithm.BlockSize = BlockSizeBits;
            algorithm.Mode = CipherMode.CBC;
            algorithm.IV = key;

            return algorithm;
        }
    }
}
