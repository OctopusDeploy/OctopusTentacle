using System;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

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

        public IScriptExecutor GetExecutor(StartScriptCommandV3Alpha startScriptCommand)
        {
            return startScriptCommand.ExecutionContext switch
            {
                LocalShellScriptExecutionContext => shellScriptExecutor.Value,
                KubernetesJobScriptExecutionContext => kubernetesJobScriptExecutor.Value,
                _ => throw new InvalidOperationException($"{startScriptCommand.GetType().Name} is not a supported IScriptExecutionContext.")
            };
        }
    }
}