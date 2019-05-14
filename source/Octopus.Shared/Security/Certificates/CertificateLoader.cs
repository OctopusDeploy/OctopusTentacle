using System.Security.Cryptography.X509Certificates;
using Octopus.Shared.Util;

namespace Octopus.Shared.Security.Certificates
{
    public static class CertificateLoader
    {
        public static X509Certificate2 FromBase64String(string certificateString)
        {
            return FromBase64String(null, certificateString);
        }
        public static X509Certificate2 FromBase64String(string thumbprint, string certificateString)
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                return CertificateEncoder.FromBase64String(thumbprint, certificateString);
            }

            return CertificateEncoder.FromBase64String(thumbprint, certificateString, StoreName.My);
        }
    }
}