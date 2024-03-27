using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesScriptPodNameExtensions
    {
        public static string ToKubernetesScriptPodName(this ScriptTicket scriptTicket) => $"octopus-script-{scriptTicket.TaskId}".ToLowerInvariant().Truncate(63);
    }
}