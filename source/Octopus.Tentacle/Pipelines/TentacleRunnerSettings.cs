#if PIPELINES_RUNNER
#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Pipelines
{
    public sealed record TentacleRunnerSettings(Uri ServerAddress, string ServerThumbprint, X509Certificate2 ClientCertificate, string RunnerId)
    {
        public const string CellId = "self-hosted";
        public const int MaxConcurrentJobs = 100;

        /// <summary>Longest wait between reconnects. Longer than the shared default, so an unreachable Server isn't retried every 30s by every Tentacle.</summary>
        public static readonly TimeSpan MaxReconnectBackoff = TimeSpan.FromMinutes(5);

        /// <summary>Pins the Server's certificate to the thumbprint Tentacle already trusts for it.</summary>
        public bool TrustsServerCertificate(X509Certificate2 certificate)
            => string.Equals(certificate.Thumbprint, ServerThumbprint, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns null and sets <paramref name="skipReason"/> (one human-readable sentence) when the runner shouldn't start.
        /// </summary>
        public static TentacleRunnerSettings? From(ITentacleConfiguration configuration, bool isKubernetesAgent, bool isWindows, out string? skipReason)
        {
            if (isKubernetesAgent)
            {
                skipReason = "The pipelines runner does not run on the Kubernetes agent.";
                return null;
            }

            if (isWindows)
            {
                skipReason = "The pipelines runner does not run on Windows.";
                return null;
            }

            var certificate = configuration.TentacleCertificate;
            if (certificate is null)
            {
                skipReason = "The pipelines runner needs the Tentacle certificate, but this Tentacle doesn't have one.";
                return null;
            }

            var server = configuration.TrustedOctopusServers.FirstOrDefault(s => s.CommunicationStyle == CommunicationStyle.TentacleActive);
            if (server is null)
            {
                skipReason = "The pipelines runner needs a polling Octopus Server, but this Tentacle doesn't trust one.";
                return null;
            }

            // Address is declared non-nullable but is null at runtime when the server was configured without one.
            if ((Uri?)server.Address is null)
            {
                skipReason = "The polling Octopus Server has no address configured, so the pipelines runner can't connect to it.";
                return null;
            }

            // Plaintext http:// (h2c) would skip the client certificate and the thumbprint pin; anything else can't carry gRPC.
            if (!string.Equals(server.Address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                skipReason = $"The polling Octopus Server's address uses {server.Address.Scheme}://, but the pipelines runner needs an https polling address.";
                return null;
            }

#pragma warning disable CS0618 // TentacleSquid is obsolete but is still the Tentacle's stable identity
            var squid = configuration.TentacleSquid;
#pragma warning restore CS0618

            skipReason = null;
            return new TentacleRunnerSettings(
                server.Address,
                server.Thumbprint,
                certificate,
                string.IsNullOrWhiteSpace(squid) ? Environment.MachineName : squid);
        }
    }
}
#endif
