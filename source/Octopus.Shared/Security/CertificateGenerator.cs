using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Octopus.Diagnostics;
#if NETFRAMEWORK
using Octopus.Shared.Internals.CertificateGeneration;
#else
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;

#endif

namespace Octopus.Shared.Security
{
    public class CertificateGenerator : ICertificateGenerator
    {
        public const int RecommendedKeyBitLength = 2048;

        public X509Certificate2 GenerateNew(string fullName, ISystemLog log)
            => Generate(fullName, true, log);

        public X509Certificate2 GenerateNewNonExportable(string fullName, ISystemLog log)
            => Generate(fullName, false, log);

#if NETFRAMEWORK
        static X509Certificate2 Generate(string fullName, bool exportable, ISystemLog log)
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
#else
        static X509Certificate2 Generate(string fullName, bool exportable, ILog log)
        {
            var random = new SecureRandom();
            var certificateGenerator = new X509V3CertificateGenerator();

            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), random);
            certificateGenerator.SetSerialNumber(serialNumber);
            certificateGenerator.SetIssuerDN(new X509Name(fullName));
            certificateGenerator.SetSubjectDN(new X509Name(fullName));
            certificateGenerator.SetNotBefore(DateTime.UtcNow.Date.AddDays(-1));
            certificateGenerator.SetNotAfter(DateTime.UtcNow.Date.AddYears(100));

            var keyGenerationParameters = new KeyGenerationParameters(random, RecommendedKeyBitLength);
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);

            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();
            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            var issuerKeyPair = subjectKeyPair;
            const string signatureAlgorithm = "SHA256WithRSA";
            var signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, issuerKeyPair.Private);
            var bouncyCert = certificateGenerator.Generate(signatureFactory);

            // Lets convert it to X509Certificate2
            X509Certificate2 certificate;

            var store = new Pkcs12StoreBuilder().Build();
            store.SetKeyEntry(string.Empty, new AsymmetricKeyEntry(subjectKeyPair.Private), new[] { new X509CertificateEntry(bouncyCert) });
            var exportpw = Guid.NewGuid().ToString();

            using (var ms = new MemoryStream())
            {
                store.Save(ms, exportpw.ToCharArray(), random);
                certificate = exportable
                    ? new X509Certificate2(ms.ToArray(), exportpw, X509KeyStorageFlags.Exportable | (X509KeyStorageFlags)32)
                    : new X509Certificate2(ms.ToArray(), exportpw, (X509KeyStorageFlags)32);
            }

            return certificate;
        }
#endif
    }
}