using Octopus.Tentacle.Client;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    internal static class TentacleClientBuilderExtensionMethods
    {
        internal static LegacyTentacleClientBuilder ForRunningTentacle(this LegacyTentacleClientBuilder tentacleClientBuilder, RunningTentacle runningTentacle)
        {
            return tentacleClientBuilder
                .WithServiceUri(runningTentacle.ServiceUri)
                .WithRemoteThumbprint(runningTentacle.Thumbprint);
        }
    }
}