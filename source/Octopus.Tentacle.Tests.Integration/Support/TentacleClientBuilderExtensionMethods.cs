using Octopus.Tentacle.Tests.Integration.TentacleClient;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TentacleClientBuilderExtensionMethods
    {
        public static TentacleClientBuilder ForRunningTentacle(this TentacleClientBuilder tentacleClientBuilder, RunningTestTentacle runningTestTentacle)
        {
            return tentacleClientBuilder
                .WithServiceUri(runningTestTentacle.ServiceUri)
                .WithRemoteThumbprint(runningTestTentacle.Thumbprint);
        }
    }
}