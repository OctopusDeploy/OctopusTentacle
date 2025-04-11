using System;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptStateStoreFactory
    {
        ScriptStateStore Create(IScriptWorkspace workspace);
    }
}