using System;

namespace Octopus.Platform.Deployment.Messages
{
    public static class WellKnownOctopusActors
    {
        public const string Logger = "Octopus.Logger";
        public const string SquidFinder = "Octopus.SquidFinder";
        public const string LifecycleEvaluator = "Octopus.LifecycleEvaluator";
        public const string PackageEvaluator = "Octopus.PackageEvaluator";
        public const string DeploymentMutex = "Octopus.DeploymentMutex";
        public const string RepositoryCleaner = "Octopus.RepositoryCleaner";
        public const string TransientSpaceDispatcher = "Octopus.TransientSpaceDispatcher";
    }
}
