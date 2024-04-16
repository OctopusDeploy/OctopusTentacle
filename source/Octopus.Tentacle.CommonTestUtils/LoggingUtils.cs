using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace Octopus.Tentacle.CommonTestUtils
{
    public static class LoggingUtils
    {
        public static string CurrentTestHash()
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(TestContext.CurrentContext.Test.FullName)))
                .Replace("=", "")
                .Replace("+", "")
                .Replace("/", "")
                .Substring(0, 10); // 64 ^ 10 is a big number, most likely we wont have collisions.
        }
    }
}