using System;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha
{
    public interface IScriptServiceV3AlphaExecutor : IAsyncScriptServiceV3Alpha
    {
        public bool ValidateExecutionContext(IScriptExecutionContext executionContext);
        bool IsRunningScript(ScriptTicket scriptTicket);
    }
}