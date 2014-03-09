using System;
using System.Security.Cryptography.X509Certificates;
using Octopus.Platform.Security.Certificates;

namespace Octopus.Shared.Licensing
{
    public class LicenseVerificationCertificateProvider : ILicenseVerificationCertificateProvider
    {
        const string License = "MIIBKTCB1KADAgECAhAgp9BixGhovUBU140MoF5gMA0GCSqGSIb3DQEBBQUAMBUxEzARBgNVBAMTCk9jdG9wdWRkbGUwIBcNMTExMjI4MDAwMDAwWhgPMjExMTEyMjkwMDAwMDBaMBUxEzARBgNVBAMTCk9jdG9wdWRkbGUwXDANBgkqhkiG9w0BAQEFAANLADBIAkEAqXjm/VA+PabwhD7GHUpCHuyQxIIzIngFzzwBuwI7iK7emjXDLnae6SIkdbNDK7A+yfALpRU91sKMw6oAl8l+7QIDAQABMA0GCSqGSIb3DQEBBQUAA0EAj8zjy+RCFnEUy+BHtXlWfoWk4RBoX/TvcyysVDvP+e7r6Jerfw2SIJS7gaa2LlKcrvk/K1f1va3HKhonPSKPSA==";
        static readonly X509Certificate2 Certificate;

        static LicenseVerificationCertificateProvider()
        {
            Certificate = CertificateEncoder.FromBase64StringPublicKeyOnly(License);
        }

        public X509Certificate2 GetCertificate()
        {
            return Certificate;
        }
    }
}