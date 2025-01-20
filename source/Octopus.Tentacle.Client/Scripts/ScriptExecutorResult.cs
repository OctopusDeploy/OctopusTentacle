using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public class ScriptExecutorResult
    {
        public ScriptStatus ScriptStatus { get; }
        public CommandContext ContextForNextCommand { get; }

        public ScriptExecutorResult(ScriptStatus scriptStatus, CommandContext contextForNextCommand)
        {
            ScriptStatus = scriptStatus;
            ContextForNextCommand = contextForNextCommand;
        }
    }
}