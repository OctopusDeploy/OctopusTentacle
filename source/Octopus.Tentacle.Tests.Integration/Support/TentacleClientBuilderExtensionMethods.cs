using Octopus.Tentacle.Tests.Integration.TentacleClient;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TentacleClientBuilderExtensionMethods
    {
        public static TentacleClientBuilder ForRunningTentacle(this TentacleClientBuilder tentacleClientBuilder, RunningTentacle runningTentacle)
        {
            return tentacleClientBuilder
                .WithServiceUri(runningTentacle.ServiceUri)
                .WithRemoteThumbprint(runningTentacle.Thumbprint);
        }
    }
}