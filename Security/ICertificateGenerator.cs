using System;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Shared.Security
{
    public interface ICertificateGenerator
    {
        X509Certificate2 GenerateNew(string fullName);
        X509Certificate2 GenerateNewNonExportable(string fullName);
    }
}