using System;

namespace Octopus.Tentacle.Scripts
{
    class ScriptExecutorFactory : IScriptExecutorFactory
    {
        readonly Lazy<LocalShellScriptExecutor> shellScriptExecutor;

        public ScriptExecutorFactory(Lazy<LocalShellScriptExecutor> shellScriptExecutor)
        {
            this.shellScriptExecutor = shellScriptExecutor;
        }

        public IScriptExecutor GetExecutor()
        {
            return shellScriptExecutor.Value;
        }
    }
}