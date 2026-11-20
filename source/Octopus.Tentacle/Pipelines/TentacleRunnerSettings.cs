#if PIPELINES_RUNNER
#nullable enable
using System;
using System.Linq;
using Octopus.Client.Model;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Pipelines
{
    public sealed record TentacleRunnerSettings(Uri ServerAddress, string RunnerId)
    {
        public const string CellId = "self-hosted";
        public const int MaxConcurrentJobs = 100;

        /// <summary>Longest wait between reconnects. Longer than the shared default, so an unreachable Server isn't retried every 30s by every Tentacle.</summary>
        public static readonly TimeSpan MaxReconnectBackoff = TimeSpan.FromMinutes(5);

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

            // gRPC only runs over http:// (h2c) or https://. Anything else (e.g. a wss:// polling address) would fail inside
            // the gateway loop after the runner had already reported itself started.
            if (server.Address.Scheme != Uri.UriSchemeHttps && server.Address.Scheme != Uri.UriSchemeHttp)
            {
                skipReason = $"The polling Octopus Server's address uses {server.Address.Scheme}://, but the pipelines runner needs an http or https polling address.";
                return null;
            }

#pragma warning disable CS0618 // TentacleSquid is obsolete but is still the Tentacle's stable identity
            var squid = configuration.TentacleSquid;
#pragma warning restore CS0618

            skipReason = null;
            return new TentacleRunnerSettings(
                server.Address,
                string.IsNullOrWhiteSpace(squid) ? Environment.MachineName : squid);
        }
    }
}
#endif
