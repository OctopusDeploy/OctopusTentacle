using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Shared.Util
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

        public static string Hash(byte[] bytes)
        {
            using (var hasher = new SHA1CryptoServiceProvider())
            {
                return Sanitize(hasher.ComputeHash(bytes));
            }
        }

        public static string Hash(string input)
        {
            using (var hasher = new SHA1CryptoServiceProvider())
            {
                return Sanitize(hasher.ComputeHash(Encoding.UTF8.GetBytes(input)));
            }
        }

        static string Sanitize(byte[] hash)
            => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}