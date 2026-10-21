using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Tentacle.CommonTestUtils
{
    public class TestCertificates
    {
        public static X509Certificate2 Tentacle;
        public static string TentaclePublicThumbprint;
        public static X509Certificate2 Server;
        public static string ServerPublicThumbprint;

        public static string TentaclePfxPath;

        static TestCertificates()
        {
            //jump through hoops to find certs because the nunit test runner is messing with directories
#pragma warning disable CS8604 // Possible null reference argument.
            var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(TestCertificates).Assembly.Location).LocalPath), "Certificates");
#pragma warning restore CS8604 // Possible null reference argument.

            TentaclePfxPath = Path.Combine(directory, "Tentacle.pfx");
            // SYSLIB0057: X509CertificateLoader is stricter about formats than these APIs, so migrating changes behaviour.
#pragma warning disable SYSLIB0057
            Tentacle = new X509Certificate2(TentaclePfxPath);
            TentaclePublicThumbprint = Tentacle.Thumbprint;

            Server = new X509Certificate2(Path.Combine(directory, "Server.pfx"));
#pragma warning restore SYSLIB0057
            ServerPublicThumbprint = Server.Thumbprint;
        }
    }
}