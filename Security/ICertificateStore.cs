using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Shared.Security
{
    public interface ICertificateStore
    {
        void Install(X509Certificate2 certificate);
        void Uninstall(X509Certificate2 certificate);
        void Uninstall(string certificateFullName);
        bool IsInstalled(params string[] certificateFullNames);
        X509Certificate2 FindCertificate(string certificateFullName);
        IEnumerable<X509Certificate2> FindCertificates(string certificateFullName);
    }
}