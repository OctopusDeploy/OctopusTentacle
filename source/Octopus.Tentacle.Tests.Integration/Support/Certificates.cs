using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    internal class Certificates
    {
        public static X509Certificate2 Tentacle;
        public static string TentaclePublicThumbprint;
        public static X509Certificate2 Server;
        public static string ServerPublicThumbprint;

        public static string TentaclePfxPath;

        static Certificates()
        {
            //jump through hoops to find certs because the nunit test runner is messing with directories
            var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(Certificates).Assembly.Location).LocalPath), "Certificates");

            TentaclePfxPath = Path.Combine(directory, "Tentacle.pfx");
            Tentacle = new X509Certificate2(TentaclePfxPath);
            TentaclePublicThumbprint = Tentacle.Thumbprint;

            Server = new X509Certificate2(Path.Combine(directory, "Server.pfx"));
            ServerPublicThumbprint = Server.Thumbprint;
        }
    }
}