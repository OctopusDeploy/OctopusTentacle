using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptExecutorFactory
    {
        IScriptExecutor GetExecutor(StartScriptCommandV3Alpha startScriptCommandV3Alpha);
    }
}