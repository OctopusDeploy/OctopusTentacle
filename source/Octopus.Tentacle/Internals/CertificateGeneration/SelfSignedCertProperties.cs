#if NETFRAMEWORK
using System;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Tentacle.Internals.CertificateGeneration
{
    public class SelfSignedCertProperties
    {
        public SelfSignedCertProperties()
        {
            var today = DateTime.Today;
            ValidFrom = today.AddDays(-1);
            ValidTo = today.AddYears(10);
            Name = new X500DistinguishedName("cn=self");
            KeyBitLength = 4096;
        }

        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public X500DistinguishedName Name { get; set; }
        public int KeyBitLength { get; set; }
        public bool IsPrivateKeyExportable { get; set; }
    }
}
#endif