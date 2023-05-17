using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptStateStoreFactory
    {
        ScriptStateStore Create(ScriptTicket ticket, IScriptWorkspace workspace);
    }
}