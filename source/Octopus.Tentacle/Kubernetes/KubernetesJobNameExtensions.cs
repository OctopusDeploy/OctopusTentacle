using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesJobNameExtensions
    {
        public static string ToKubernetesJobName(this ScriptTicket scriptTicket) => $"octopus-job-{scriptTicket.TaskId}".ToLowerInvariant();
    }
}