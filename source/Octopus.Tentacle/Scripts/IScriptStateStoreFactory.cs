using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptStateStoreFactory
    {
        ScriptStateStore Get(ScriptTicket ticket, IScriptWorkspace workspace);
    }
}