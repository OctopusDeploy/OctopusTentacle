using System;
using System.Collections.Generic;
using System.IdentityModel.Selectors;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using log4net;

namespace Octopus.Shared.Security
{
    public class CertificateValidator : X509CertificateValidator
    {
        readonly HashSet<string> trustedThumbprints;
        readonly ILog log;

        public CertificateValidator(IEnumerable<string> trustedThumbprints, ILog log)
        {
            this.trustedThumbprints = new HashSet<string>(trustedThumbprints);
            this.log = log;
        }

        public override void Validate(X509Certificate2 certificate)
        {
            var key = certificate.Thumbprint;
            if (key != null && trustedThumbprints.Contains(key))
                return;

            log.Error("Rejected communication because it was signed with the wrong certificate; the thumbprint of the certificate in the request was: " + key);
            throw new SecurityException("The certificate thumbprint that you provided is not in our list of trusted certificates. You provided: " + key);
        }
    }
}