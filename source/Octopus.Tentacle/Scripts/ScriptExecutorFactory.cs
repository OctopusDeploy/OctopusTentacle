using System;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptExecutorFactory : IScriptExecutorFactory
    {
        readonly Lazy<ShellScriptExecutor> shellScriptExecutor;

        public ScriptExecutorFactory(Lazy<ShellScriptExecutor> shellScriptExecutor)
        {
            this.shellScriptExecutor = shellScriptExecutor;
        }

        public IScriptExecutor GetExecutor()
        {
            return shellScriptExecutor.Value;
        }
    }
}