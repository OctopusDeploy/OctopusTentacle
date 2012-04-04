using System;
using System.Collections.Generic;
using System.IO;
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
            var results = new List<X509Certificate2>();

            var encoded = configuration.Get("Cert-" + certificateFullName);
            if (string.IsNullOrWhiteSpace(encoded))
                return results;

            var exported = Convert.FromBase64String(encoded);

            var file = Path.Combine(Path.GetTempPath(), "Octo-" + Guid.NewGuid());
            try
            {
                File.WriteAllBytes(file, exported);

                var certificate = new X509Certificate2(file, (string) null, X509KeyStorageFlags.Exportable);
                results.Add(certificate);
            }
            finally
            {
                File.Delete(file);
            }
                
            return results;
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