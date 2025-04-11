using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Maintenance
{
    public interface IRunningScriptReporter
    {
        bool IsRunningScript(ScriptTicket ticket);
    }
}