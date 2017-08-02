using System;
using System.Security.Cryptography.X509Certificates;
using Octopus.Diagnostics;

namespace Octopus.Shared.Security
{
    public interface ICertificateGenerator
    {
        X509Certificate2 GenerateNew(string fullName, ILog log);
        X509Certificate2 GenerateNewNonExportable(string fullName, ILog log);
    }
}