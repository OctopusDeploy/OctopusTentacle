using System;
using System.Linq;
using System.Security.Cryptography;

namespace Octopus.Shared.Security
{
    public class Aes256EncryptionAlgorithm : SymmetricEncryptionAlgorithm
    {
        protected override SymmetricAlgorithm CreateAlgorithm(byte[] salt)
        {
            var algorithm = CryptoConfig.AllowOnlyFipsAlgorithms
                                ? (Aes)new AesCryptoServiceProvider()
                                : new AesManaged();

            algorithm.Padding = PaddingMode.PKCS7;
            algorithm.KeySize = 256;
            algorithm.Key = salt;
            algorithm.BlockSize = 128;
            algorithm.Mode = CipherMode.CBC;
            algorithm.IV = salt.Take(16).ToArray();

            return algorithm;
        }
    }
}