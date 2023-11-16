using System;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Scripts.Kubernetes;

namespace Octopus.Tentacle.Scripts
{
    class ScriptExecutorFactory : IScriptExecutorFactory
    {
        readonly Lazy<LocalShellScriptExecutor> shellScriptExecutor;
        readonly Lazy<KubernetesJobScriptExecutor> kubernetesJobScriptExecutor;

        public ScriptExecutorFactory(Lazy<LocalShellScriptExecutor> shellScriptExecutor, Lazy<KubernetesJobScriptExecutor> kubernetesJobScriptExecutor)
        {
            this.shellScriptExecutor = shellScriptExecutor;
            this.kubernetesJobScriptExecutor = kubernetesJobScriptExecutor;
        }

        public IScriptExecutor GetExecutor()
        {
            return KubernetesJobsConfig.UseJobs switch
            {
                true => kubernetesJobScriptExecutor.Value,
                false => shellScriptExecutor.Value
            };
        }
    }
}