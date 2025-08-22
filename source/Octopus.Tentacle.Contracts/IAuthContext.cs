using System;

namespace Octopus.Tentacle.Contracts
{
    public interface IAuthContext
    {
    }

    public class KubernetesAgentAuthContext : IAuthContext
    {
        public KubernetesAgentAuthContext(string? projectSlug, string? environmentSlug, string? tenantSlug, string? stepSlug)
        {
            ProjectSlug = projectSlug;
            EnvironmentSlug = environmentSlug;
            TenantSlug = tenantSlug;
            StepSlug = stepSlug;
        }

        public string? ProjectSlug { get; }
        public string? EnvironmentSlug { get; }
        public string? TenantSlug { get; }
        public string? StepSlug { get; }
    }
}