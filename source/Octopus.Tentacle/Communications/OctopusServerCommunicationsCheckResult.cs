using System;
using System.Net.Security;

namespace Octopus.Tentacle.Communications
{
    public sealed class OctopusServerCommunicationsCheckResult
    {
        public OctopusServerCommunicationsCheckResult(string thumbprint, SslPolicyErrors certificatePolicyErrors)
        {
            Thumbprint = thumbprint;
            CertificatePolicyErrors = certificatePolicyErrors;
        }

        public string Thumbprint { get; }

        public SslPolicyErrors CertificatePolicyErrors { get; }

        public bool CertificateWasValidated => CertificatePolicyErrors == SslPolicyErrors.None;
    }
}
