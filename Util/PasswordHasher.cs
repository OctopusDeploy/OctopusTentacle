using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Shared.Util
{
    /// <summary>
    /// Secure password hasher using the inbuilt .NET implementation of PBKDF2 (<see cref="Rfc2898DeriveBytes" />). Simialr
    /// to BCrypt, the hash string returned
    /// contains the number of iterations and the salt to use. If the number of iterations change in future, old passwords
    /// can still be verified.
    /// </summary>
    public class PasswordHasher
    {
        const int SaltSize = 16;
        const int HashIterations = 1000;
        static readonly RandomNumberGenerator RandomSource = RandomNumberGenerator.Create();

        /// <summary>
        /// Generates a new salt, and then hashes the salt and password. The resulting string contains the number of iterations
        /// used, the generated salt, and the hashed password.
        /// Do NOT use this function when comparing a password hash since it will get a new salt each time. Instead, use
        /// <see cref="VerifyPassword" />.
        /// </summary>
        /// <param name="plainTextPassword">The new plain text password to hash.</param>
        public static string HashPassword(string plainTextPassword)
        {
            var salt = GenerateSalt();
            const int iterations = HashIterations;
            var hashedPassword = Pbkdf(plainTextPassword, iterations, salt);
            return EncodeHashString(iterations, salt, hashedPassword);
        }

        /// <summary>
        /// Verifies a password against the current password hash. Since the hash contains the number of iterations and the
        /// salt, this is all the information we need to compare.
        /// </summary>
        /// <param name="candidatePlainTextPassword">The user-provided password to check.</param>
        /// <param name="knownHash">The hash of our existing password.</param>
        /// <returns><c>true</c> if the password is valid; otherwise <c>false</c>.</returns>
        public static bool VerifyPassword(string candidatePlainTextPassword, string knownHash)
        {
            int iterations;
            byte[] salt;
            byte[] hash;

            if (DecodeHashString(knownHash, out iterations, out salt, out hash))
            {
                var candidateHash = Pbkdf(candidatePlainTextPassword, iterations, salt);

                return candidateHash.SequenceEqual(hash);
            }

            return false;
        }

        static byte[] GenerateSalt()
        {
            var ret = new byte[SaltSize];

            lock (RandomSource)
                RandomSource.GetBytes(ret);

            return ret;
        }

        static string EncodeHashString(int iterations, byte[] salt, byte[] hashedPassword)
        {
            return iterations.ToString("X") + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hashedPassword);
        }

        static bool DecodeHashString(string hash, out int iterations, out byte[] salt, out byte[] hashedPassword)
        {
            var parts = hash.Split('$');
            if (parts.Length == 3)
            {
                iterations = int.Parse(parts[0], NumberStyles.HexNumber);
                salt = Convert.FromBase64String(parts[1]);
                hashedPassword = Convert.FromBase64String(parts[2]);
                return true;
            }

            iterations = 0;
            salt = null;
            hashedPassword = null;
            return false;
        }

        static byte[] Pbkdf(string plainTextPassword, int iterations, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(plainTextPassword), salt, iterations))
            {
                var key = pbkdf2.GetBytes(24);
                return key;
            }
        }
    }
}