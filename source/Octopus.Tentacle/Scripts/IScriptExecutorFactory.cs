namespace Octopus.Tentacle.Scripts
{
    public interface IScriptExecutorFactory
    {
        IScriptExecutor GetExecutor();
    }
}