using System;
using System.IO;
using System.Security.Cryptography;

namespace Octopus.Tentacle.Util
{
    public static class HashCalculator
    {
        public static string Hash(Stream stream)
        {
            using var hasher = SHA1.Create();
            return Sanitize(hasher.ComputeHash(stream));
        }

        static string Sanitize(byte[] hash)
            => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}