using System;
using System.Security.Cryptography.X509Certificates;
using Octopus.Diagnostics;
using Octopus.Shared.Internals.CertificateGeneration;

namespace Octopus.Shared.Security
{
    public class CertificateGenerator : ICertificateGenerator
    {
        public const int RecommendedKeyBitLength = 2048;

        public X509Certificate2 GenerateNew(string fullName, ILog log)
        {
            return Generate(fullName, true, log);
        }

        public X509Certificate2 GenerateNewNonExportable(string fullName, ILog log)
        {
            return Generate(fullName, false, log);
        }

        static X509Certificate2 Generate(string fullName, bool exportable, ILog log)
        {
            using (var cryptography = new CryptContext(log))
            {
                cryptography.Open();

                return cryptography.CreateSelfSignedCertificate(
                    new SelfSignedCertProperties
                    {
                        IsPrivateKeyExportable = exportable,
                        KeyBitLength = RecommendedKeyBitLength,
                        Name = new X500DistinguishedName(fullName),
                        ValidFrom = DateTime.Today.AddDays(-1),
                        ValidTo = DateTime.Today.AddYears(100)
                    });
            }
        }
    }
}