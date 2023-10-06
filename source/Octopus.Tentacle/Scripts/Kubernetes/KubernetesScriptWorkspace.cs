using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts.Kubernetes
{
    public class KubernetesScriptWorkspace : BashScriptWorkspace
    {
        public KubernetesScriptWorkspace(ScriptTicket scriptTicket, string workingDirectory, IOctopusFileSystem fileSystem, SensitiveValueMasker sensitiveValueMasker)
            : base(scriptTicket, workingDirectory, fileSystem, sensitiveValueMasker)
        {
        }
    }
}