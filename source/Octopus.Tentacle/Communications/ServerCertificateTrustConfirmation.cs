using System;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Communications
{
    public class ServerCertificateTrustConfirmation : IServerCertificateTrustConfirmation
    {
        readonly ISystemLog log;
        readonly IPrompt prompt;

        public ServerCertificateTrustConfirmation(ISystemLog log, IPrompt prompt)
        {
            this.log = log;
            this.prompt = prompt;
        }

        public void EnsureCertificateIsTrusted(Uri serverAddress, OctopusServerCommunicationsCheckResult checkResult, string? expectedThumbprint)
        {
            // An expected thumbprint is checked even when the certificate validated, because the operator has told us
            // exactly which certificate they mean and anything else is worth failing over.
            if (!string.IsNullOrWhiteSpace(expectedThumbprint))
            {
                var expected = expectedThumbprint!.Trim();

                if (!string.Equals(expected, checkResult.Thumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ControlledFailureException(
                        $"The certificate presented by {serverAddress} has thumbprint {checkResult.Thumbprint}, which does not match the thumbprint {expected} given by --server-web-socket-thumbprint. " +
                        "Either that thumbprint is wrong, or something other than the expected endpoint answered at that address.");
                }

                log.Verbose($"The certificate presented by {serverAddress} matches the thumbprint given by --server-web-socket-thumbprint. Trusting {serverAddress} using thumbprint {checkResult.Thumbprint}.");
                return;
            }

            if (checkResult.CertificateWasValidated)
            {
                log.Verbose($"The certificate presented by {serverAddress} was validated successfully. Trusting {serverAddress} using thumbprint {checkResult.Thumbprint}.");
                return;
            }

            var problem = $"The certificate presented by {serverAddress} could not be validated ({checkResult.CertificatePolicyErrors}). Its thumbprint is {checkResult.Thumbprint}.";

            log.Warn(problem);

            if (!prompt.CanPrompt)
            {
                throw new ControlledFailureException(
                    $"{problem} Because this certificate could not be validated, it is not possible to confirm that it belongs to your Octopus Server rather than to an attacker positioned between this machine and the server. " +
                    "Check that this thumbprint matches your server's websockets certificate - the one configured wherever TLS is terminated for the websockets endpoint, such as IIS, HTTP.SYS or a reverse proxy, and not the Octopus Server's Tentacle Communications certificate. " +
                    "Once you have confirmed it, re-run this command with --server-web-socket-thumbprint=<thumbprint> to trust it.");
            }

            var context = $"{problem}{Environment.NewLine}" +
                $"Because this certificate could not be validated, it is not possible to confirm that it belongs to your Octopus Server rather than to an attacker positioned between this machine and the server.{Environment.NewLine}" +
                $"The certificate presented is the one configured wherever TLS is terminated for the websockets endpoint, not the Octopus Server's Tentacle Communications certificate.{Environment.NewLine}" +
                "Check that this thumbprint matches your server's websockets certificate before continuing.";

            if (!prompt.Confirm(context, "Trust this certificate?"))
            {
                throw new ControlledFailureException($"The certificate presented by {serverAddress} was not trusted, so the Octopus Server has not been configured.");
            }

            log.Warn($"The operator confirmed that the certificate with thumbprint {checkResult.Thumbprint} should be trusted, despite it failing validation.");
        }
    }
}
