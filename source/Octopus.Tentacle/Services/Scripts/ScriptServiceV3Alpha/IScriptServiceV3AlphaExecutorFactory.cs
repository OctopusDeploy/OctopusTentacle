namespace Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha
{
    public interface IScriptServiceV3AlphaExecutorFactory
    {
        IScriptServiceV3AlphaExecutor GetExecutor();
    }
}