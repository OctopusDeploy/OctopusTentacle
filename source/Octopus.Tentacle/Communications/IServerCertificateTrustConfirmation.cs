using System;

namespace Octopus.Tentacle.Communications
{
    public interface IServerCertificateTrustConfirmation
    {
        void EnsureCertificateIsTrusted(Uri serverAddress, OctopusServerCommunicationsCheckResult checkResult, string? expectedThumbprint);
    }
}
