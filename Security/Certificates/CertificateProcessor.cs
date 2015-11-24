using System;
using System.Security.Cryptography.X509Certificates;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Security.Certificates
{
    public class CertificateProcessor
    {
        static readonly ILog Log = Diagnostics.Log.System();

        public static X509Certificate2 GetCertificateFromPfx(string certificateFilePath, string password)
        {
            X509Certificate2Collection certificates = new X509Certificate2Collection();
            if (string.IsNullOrEmpty(password))
            {
                Log.InfoFormat($"Importing the certificate stored in PFX file in {certificateFilePath}...");
                certificates.Import(certificateFilePath, string.Empty, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            }
            else
            {
                Log.InfoFormat($"Importing the certificate stored in PFX file in {certificateFilePath} using the provided password...");
                certificates.Import(certificateFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            }

            if (certificates.Count == 0)
                throw new Exception($"The PFX file ({certificateFilePath}) does not contain any certificates.");

            if (certificates.Count > 1)
                Log.InfoFormat("PFX file contains multiple certificates, taking the first one.", certificateFilePath);

            var x509Certificate = certificates[0];
            return x509Certificate;
        }
    }
}