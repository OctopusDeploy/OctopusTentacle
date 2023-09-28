using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptExecutorFactory : IScriptExecutorFactory
    {
        private readonly Lazy<ShellScriptExecutor> shellScriptExecutor;
        private readonly Lazy<KubernetesJobScriptExecutor> kubernetesJobScriptExecutor;

        public ScriptExecutorFactory(Lazy<ShellScriptExecutor> shellScriptExecutor, Lazy<KubernetesJobScriptExecutor> kubernetesJobScriptExecutor)
        {
            this.shellScriptExecutor = shellScriptExecutor;
            this.kubernetesJobScriptExecutor = kubernetesJobScriptExecutor;
        }

        public IScriptExecutor GetExecutor()
        {
            return PlatformDetection.IsRunningInKubernetes switch
            {
                true => kubernetesJobScriptExecutor.Value,
                false => shellScriptExecutor.Value
            };
        }
    }
}