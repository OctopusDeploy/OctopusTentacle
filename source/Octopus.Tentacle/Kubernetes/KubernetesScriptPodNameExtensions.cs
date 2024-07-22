using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesScriptPodNameExtensions
    {
        public const string OctopusScriptPodNamePrefix = "octopus-script";
        public static string ToKubernetesScriptPodName(this ScriptTicket scriptTicket) => $"{OctopusScriptPodNamePrefix}-{scriptTicket.TaskId}".ToLowerInvariant().Truncate(63);
    }
}