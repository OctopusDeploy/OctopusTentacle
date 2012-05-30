using System;
using System.Collections.Generic;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using log4net;

namespace Octopus.Shared.Security
{
    public class CertificateValidator : X509CertificateValidator
    {
        readonly HashSet<string> trustedThumbprints;
        readonly CertificateValidationDirection direction;
        readonly ILog log;

        public CertificateValidator(IEnumerable<string> trustedThumbprints, CertificateValidationDirection direction, ILog log)
        {
            this.trustedThumbprints = new HashSet<string>(trustedThumbprints);
            this.direction = direction;
            this.log = log;
        }

        public override void Validate(X509Certificate2 certificate)
        {
            var key = certificate.Thumbprint;
            if (key != null && trustedThumbprints.Contains(key))
                return;

            log.Error("Could not establish a trust relationship because the other party was using the wrong certificate; the thumbprint of the certificate they provided was: " + key + " while we would have accepted: " + string.Join(", ", trustedThumbprints));
                
            if (direction == CertificateValidationDirection.TheyCalledUs)
            {
                throw new SecurityTokenValidationException("The certificate thumbprint provided by the remote client is not in our list of trusted certificates. We can't accept requests from that client. The client identified itself with the thumbprint: " + key);                
            }
            
            throw new SecurityTokenValidationException("The certificate thumbprint given by the remote server is not what we expected. The remote server identified as: " + key);
        }
    }
}