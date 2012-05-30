using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Shared.Security
{
    public static class CertificateEncoder
    {
        public static X509Certificate2 FromBase64String(string certificateString)
        {
            var raw = Convert.FromBase64String(certificateString);
            var file = Path.Combine(Path.GetTempPath(), "Octo-" + Guid.NewGuid());
            try
            {
                File.WriteAllBytes(file, raw);
                return new X509Certificate2(file, (string)null, X509KeyStorageFlags.Exportable);
            }
            finally
            {
                File.Delete(file);
            }    
        }

        public static string ToBase64String(X509Certificate2 certificate)
        {
            var exported = certificate.Export(X509ContentType.Pkcs12);
            var encoded = Convert.ToBase64String(exported);
            return encoded;
        }
    }
}