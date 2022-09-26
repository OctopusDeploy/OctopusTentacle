using System;
using System.IO;
using System.Security.Cryptography;

namespace Octopus.Tentacle.Util
{
    public static class HashCalculator
    {
        public static string Hash(Stream stream)
        {
            using (var hasher = new SHA1CryptoServiceProvider())
            {
                return Sanitize(hasher.ComputeHash(stream));
            }
        }

        private static string Sanitize(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}