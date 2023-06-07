using System;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptStateStore
    {
        bool Exists();
        ScriptState Create();
        void Save(ScriptState state);
        ScriptState Load();
    }
}