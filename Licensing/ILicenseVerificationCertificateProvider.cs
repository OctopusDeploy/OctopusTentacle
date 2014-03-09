using System;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Shared.Licensing
{
    public interface ILicenseVerificationCertificateProvider
    {
        X509Certificate2 GetCertificate();
    }
}