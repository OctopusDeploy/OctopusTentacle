using System;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Core.Services.Scripts.StateStore
{
    public interface IScriptStateStore
    {
        bool Exists();
        ScriptState Create();
        void Save(ScriptState state);
        ScriptState Load();
    }
}