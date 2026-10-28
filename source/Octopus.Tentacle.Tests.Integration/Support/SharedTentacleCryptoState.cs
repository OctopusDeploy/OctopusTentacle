using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Security.Certificates;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    /// <summary>
    /// Creates the crypto state every Tentacle in the run shares, before any test starts one.
    ///
    /// Each test Tentacle has its own configuration directory, but on Linux they all share the
    /// fallback machine key (used when /etc/machine-id is unusable) and the user's certificate
    /// store. Both are created on first use, and creating them from several Tentacles at once
    /// races: one can replace the key another just encrypted with, or two can write the same
    /// certificate file into the store. Once both exist, Tentacles only read them.
    ///
    /// This goes through the same code a Tentacle uses at startup, so it only covers Tentacles
    /// running as this user.
    /// </summary>
    public static class SharedTentacleCryptoState
    {
        static string TestThumbprint => TestCertificates.TentaclePublicThumbprint;

        public static void Initialize(ILogger logger)
        {
            var log = new SystemLog();

            // Stored the way import-certificate stores it: encrypted with the machine key, which
            // creates the fallback key if it is needed and does not exist yet.
            var pfxPath = TestCertificates.TentaclePfxPath;
            var certificate = CertificateEncoder.FromPfxFile(pfxPath, string.Empty, log);
            var encoded = CertificateEncoder.ToBase64String(certificate);
            var encrypted = MachineKeyEncryptor.Current.Encrypt(encoded);

            // Loaded the way a Tentacle loads it at startup, which adds it to the store.
            var decrypted = MachineKeyEncryptor.Current.Decrypt(encrypted);
            var loaded = TentacleConfiguration.LoadTentacleCertificate(
                TestThumbprint,
                decrypted,
                log);

            var comparison = StringComparison.OrdinalIgnoreCase;
            if (!string.Equals(loaded.Thumbprint, TestThumbprint, comparison))
            {
                throw new Exception(
                    $"Loaded the certificate with thumbprint '{loaded.Thumbprint}' " +
                    $"but the tests use '{TestThumbprint}'.");
            }

            VerifyTheStoreHoldsTheCertificateWithItsPrivateKey();

            logger.Information(
                "Initialized the shared Tentacle crypto state for {UserName}",
                Environment.UserName);
        }

        // A Tentacle only reuses the stored certificate when it comes back with its private key.
        // Otherwise it adds it to the store again, which is the concurrent write this avoids.
        static void VerifyTheStoreHoldsTheCertificateWithItsPrivateKey()
        {
            var storeName = CertificateEncoder.CertificateStoreName;
            using var store = new X509Store(storeName, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            var stored = store.Certificates
                .Find(X509FindType.FindByThumbprint, TestThumbprint, validOnly: false)
                .OfType<X509Certificate2>()
                .FirstOrDefault(c => c.HasPrivateKey);

            if (stored == null)
            {
                throw new Exception(
                    $"The test certificate with its private key is not in the {storeName} store " +
                    $"of {Environment.UserName}, so each Tentacle the tests start would add it " +
                    "again.");
            }
        }
    }
}
