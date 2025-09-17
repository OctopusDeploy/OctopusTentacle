using System;

namespace Octopus.Tentacle.Contracts
{
    public class KubernetesAgentAuthContext
    {
        public KubernetesAgentAuthContext(string projectSlug, string environmentSlug, string? tenantSlug, string stepSlug, string spaceSlug)
        {
            ProjectSlug = projectSlug;
            EnvironmentSlug = environmentSlug;
            TenantSlug = tenantSlug;
            StepSlug = stepSlug;
            SpaceSlug = spaceSlug;
        }

        public string ProjectSlug { get; }
        public string EnvironmentSlug { get; }
        public string? TenantSlug { get; }
        public string StepSlug { get; }
        public string SpaceSlug { get; }
    }
}