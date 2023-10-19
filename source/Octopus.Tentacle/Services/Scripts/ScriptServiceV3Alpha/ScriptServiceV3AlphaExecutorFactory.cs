using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha
{
    public class ScriptServiceV3AlphaExecutorFactory : IScriptServiceV3AlphaExecutorFactory
    {
        readonly Lazy<ShellScriptServiceV3AlphaExecutor> shellExecutor;
        readonly Lazy<KubernetesJobScriptServiceV3AlphaExecutor> kubernetesJobExecutor;

        public ScriptServiceV3AlphaExecutorFactory(Lazy<ShellScriptServiceV3AlphaExecutor> shellExecutor, Lazy<KubernetesJobScriptServiceV3AlphaExecutor> kubernetesJobExecutor)
        {
            this.shellExecutor = shellExecutor;
            this.kubernetesJobExecutor = kubernetesJobExecutor;
        }

        public IScriptServiceV3AlphaExecutor GetExecutor()
        {
            return PlatformDetection.Kubernetes.IsRunningInKubernetes switch
            {
                true => kubernetesJobExecutor.Value,
                false => shellExecutor.Value
            };
        }
    }
}