using System;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptExecutorFactory : IScriptExecutorFactory
    {
        private readonly Lazy<ITentacleConfiguration> configuration;
        private readonly Lazy<ShellScriptExecutor> shellScriptExecutor;
        private readonly Lazy<KubernetesJobScriptExecutor> kubernetesJobScriptExecutor;

        public ScriptExecutorFactory(Lazy<ITentacleConfiguration> configuration,
            Lazy<ShellScriptExecutor> shellScriptExecutor,
            Lazy<KubernetesJobScriptExecutor> kubernetesJobScriptExecutor)
        {
            this.configuration = configuration;
            this.shellScriptExecutor = shellScriptExecutor;
            this.kubernetesJobScriptExecutor = kubernetesJobScriptExecutor;
        }

        public IScriptExecutor GetExecutor()
        {
            return GetExecutor(configuration.Value.ScriptExecutor);
        }

        public IScriptExecutor GetExecutor(ScriptExecutor scriptExecutor)
        {
            return scriptExecutor switch
            {
                ScriptExecutor.Shell => shellScriptExecutor.Value,
                ScriptExecutor.KubernetesJob => kubernetesJobScriptExecutor.Value,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}