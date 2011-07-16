using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Security
{
    public class CertificateStore : IDisposable, ICertificateStore
    {
        readonly IGlobalConfiguration configuration;

        public CertificateStore(IGlobalConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public bool IsInstalled(params string[] certificateFullNames)
        {
            return certificateFullNames.All(name => FindCertificates(name).Count() > 0);
        }

        public void Install(X509Certificate2 certificate)
        {
            var exported = certificate.Export(X509ContentType.Pkcs12);
            var encoded = Convert.ToBase64String(exported);
            configuration.Set("Cert-" + certificate.SubjectName.Name, encoded);
        }

        public void Uninstall(X509Certificate2 certificate)
        {
            if (certificate == null)
                return;

            configuration.Set("Cert-" + certificate.SubjectName.Name, "");
        }

        public void Uninstall(string certificateFullName)
        {
            foreach (var cert in FindCertificates(certificateFullName).ToList())
            {
                Uninstall(cert);
            }
        }

        public IEnumerable<X509Certificate2> FindCertificates(string certificateFullName)
        {
            var encoded = configuration.Get("Cert-" + certificateFullName);
            if (string.IsNullOrWhiteSpace(encoded))
                yield break;

            var exported = Convert.FromBase64String(encoded);

            var certificate = new X509Certificate2(exported, (string) null, X509KeyStorageFlags.Exportable);
            yield return certificate;
        }

        public X509Certificate2 FindCertificate(string certificateFullName)
        {
            return FindCertificates(certificateFullName).FirstOrDefault();
        }

        public void Dispose()
        {
        }
    }
}