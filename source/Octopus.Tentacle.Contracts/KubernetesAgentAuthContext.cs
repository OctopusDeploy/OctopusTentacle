using System;

namespace Octopus.Tentacle.Contracts
{
    public class KubernetesAgentAuthContext
    {
        public KubernetesAgentAuthContext(
            string spaceSlug,
            string projectSlug,
            string? projectGroupSlug,
            string environmentSlug,
            string? tenantSlug,
            string stepSlug)
        {
            SpaceSlug = spaceSlug;
            ProjectSlug = projectSlug;
            ProjectGroupSlug = projectGroupSlug;
            EnvironmentSlug = environmentSlug;
            TenantSlug = tenantSlug;
            StepSlug = stepSlug;
        }

        public string SpaceSlug { get; }
        public string ProjectSlug { get; }
        public string? ProjectGroupSlug { get; } // This is only nullable for backwards compatibility
        public string EnvironmentSlug { get; }
        public string? TenantSlug { get; }
        public string StepSlug { get; }
    }
}