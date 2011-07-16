using System;
using System.IdentityModel.Selectors;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using log4net;

namespace Octopus.Shared.Security
{
    public class CertificateValidator : X509CertificateValidator
    {
        readonly X509Certificate2 authorizedPublicKey;
        readonly ILog log;

        public CertificateValidator(X509Certificate2 authorizedPublicKey, ILog log)
        {
            this.authorizedPublicKey = authorizedPublicKey;
            this.log = log;
        }

        public override void Validate(X509Certificate2 certificate)
        {
            if (authorizedPublicKey == null)
                // Feature is disabled
                return;

            var key = certificate.GetPublicKeyString();
            if (key == authorizedPublicKey.GetPublicKeyString())
                return;

            log.Error("Rejected communication because it was signed with the wrong certificate; the public key of the certificate was: " + key);
            throw new SecurityException("Invalid certificate; rejected");
        }
    }
}