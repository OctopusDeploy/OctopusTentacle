using System;
using System.Security.Cryptography.X509Certificates;
using Octopus.Shared.Security.CertificateGeneration;

namespace Octopus.Shared.Security
{
    public class CertificateGenerator : ICertificateGenerator
    {
        public X509Certificate2 GenerateNew(string fullName)
        {
            return Generate(fullName, true);
        }

        public X509Certificate2 GenerateNewNonExportable(string fullName)
        {
            return Generate(fullName, false);
        }

        static X509Certificate2 Generate(string fullName, bool exportable)
        {
            using (var cryptography = new CryptContext())
            {
                cryptography.Open();

                return cryptography.CreateSelfSignedCertificate(
                    new SelfSignedCertProperties
                        {
                            IsPrivateKeyExportable = exportable,
                            KeyBitLength = 512,
                            Name = new X500DistinguishedName(fullName),
                            ValidFrom = DateTime.Today.AddDays(-1),
                            ValidTo = DateTime.Today.AddYears(100)
                        });
            }
        }
    }
}