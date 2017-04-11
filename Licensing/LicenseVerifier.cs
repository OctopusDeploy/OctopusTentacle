using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Octopus.Shared.Licensing
{
    /// <summary>
    /// Checks that a license key has the correct signature and hasn't been tampered with.
    /// </summary>
    public class LicenseVerifier : ILicenseVerifier
    {
        // The encoding used to generate signatures has changed over time which prevents us from validating old signatures 
        // unless we use the right encoding
        static readonly Encoding[] Encodings = {Encoding.Default, Encoding.GetEncoding(1252), Encoding.ASCII, Encoding.UTF8, Encoding.UTF32};
        readonly ILicenseVerificationCertificateProvider certificateProvider;

        public LicenseVerifier(ILicenseVerificationCertificateProvider certificateProvider)
        {
            this.certificateProvider = certificateProvider;
        }

        public bool VerifySignature(XDocument document)
        {
            if (document.Root == null)
            {
                return false;
            }

            var elements = string.Join("|", document.Root.Elements().Select(x => x.Name + ":" + x.Value.Trim()));
            var expectedSignature = document.Root.Attribute("Signature").Value;

            return Encodings.Any(encoding =>
                CheckHash(encoding, elements, expectedSignature));
        }

        public bool CheckHash(Encoding encoding, string joinedElements, string expectedSignature)
        {
            if (encoding == null) return false;

            byte[] hash;
            using (var hasher = new SHA1CryptoServiceProvider())
            {
                hash = hasher.ComputeHash(encoding.GetBytes(joinedElements));
            }

            // Once loaded this will use the same instance of the RSACryptoServiceProvider
            var rsa = (RSACryptoServiceProvider) certificateProvider.GetCertificate().PublicKey.Key;
            return rsa.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA1"), Convert.FromBase64String(expectedSignature));
        }
    }
}