using System;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Core.Services.Scripts.StateStore
{
    public interface IScriptStateStoreFactory
    {
        ScriptStateStore Create(IScriptWorkspace workspace);
    }
}