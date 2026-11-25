using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Security.Certificates;
using Octopus.Tentacle.Util;
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

            CreateTheFallbackKeyEarlierVersionsShare(log);

            // Encoded the way import-certificate stores it, then loaded the way a Tentacle loads it
            // at startup, which adds it to the store.
            var pfxPath = TestCertificates.TentaclePfxPath;
            var certificate = CertificateEncoder.FromPfxFile(pfxPath, string.Empty, log);
            var encoded = CertificateEncoder.ToBase64String(certificate);
            var loaded = TentacleConfiguration.LoadTentacleCertificate(
                TestThumbprint,
                encoded,
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

        // Earlier versions create it on first use when /etc/machine-id is unusable, and the
        // compatibility tests start several of them at once.
        static void CreateTheFallbackKeyEarlierVersionsShare(SystemLog log)
        {
            if (CommonTestUtils.PlatformDetection.IsRunningOnWindows)
            {
                return;
            }

            var fileSystem = new OctopusPhysicalFileSystem(log);
            try
            {
                new LinuxMachineIdKey(fileSystem).Load();
            }
            catch (InvalidOperationException)
            {
                CreateLegacyMachineKeyIfMissing(fileSystem, LinuxGeneratedMachineKey.LegacyKeyFilePathForThisHost);
            }
        }

        [UnsupportedOSPlatform("windows")]
        static void CreateLegacyMachineKeyIfMissing(OctopusPhysicalFileSystem fileSystem, string path)
        {
            if (fileSystem.FileExists(path))
            {
                return;
            }

            using var aes = Aes.Create();
            aes.GenerateIV();
            aes.GenerateKey();
            var contents = Convert.ToBase64String(aes.Key) + "." + Convert.ToBase64String(aes.IV);
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(path)!);
            fileSystem.TryAtomicCreateFileWithOnlyUserReadWritePermissions(path, Encoding.ASCII.GetBytes(contents));
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
