using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    internal class Certificates
    {
        public static X509Certificate2 Tentacle;
        public static string TentaclePublicThumbprint;
        public static string TentaclePfxPath;
        
        public static X509Certificate2 Server;
        public static string ServerPublicThumbprint;
        public static string ServerPfxPath;
        
        public static TestCertificate ServerCertificate => new(ServerPfxPath, Server, ServerPublicThumbprint);
         
        public static TestCertificate TentacleCertificate => new(TentaclePfxPath, Tentacle, TentaclePublicThumbprint);

        public static string BadPfxPath;
        public static X509Certificate2 Bad;
        public static string BadThumbprint;

        public static TestCertificate BadCertificate => new(BadPfxPath, Bad, BadThumbprint);
        

        static Certificates()
        {
            //jump through hoops to find certs because the nunit test runner is messing with directories
            var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(Certificates).Assembly.Location).LocalPath), "Certificates");

            TentaclePfxPath = Path.Combine(directory, "Tentacle.pfx");
            Tentacle = new X509Certificate2(TentaclePfxPath);
            TentaclePublicThumbprint = Tentacle.Thumbprint;

            ServerPfxPath = Path.Combine(directory, "Server.pfx");
            Server = new X509Certificate2(ServerPfxPath);
            ServerPublicThumbprint = Server.Thumbprint;
            
            BadPfxPath = Path.Combine(directory, "bad-certificate.pfx");
            Bad = new X509Certificate2(BadPfxPath);
            BadThumbprint = Bad.Thumbprint;
        }
    }

    public class TestCertificate
    {
        public string PfxPath;
        public X509Certificate2 Certificate;
        public string Thumbprint;

        public TestCertificate(string pfxPath, X509Certificate2 certificate, string thumbprint)
        {
            PfxPath = pfxPath;
            Certificate = certificate;
            Thumbprint = thumbprint;
        }
    }
}