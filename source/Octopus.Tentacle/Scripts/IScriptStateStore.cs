using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptStateStore
    {
        bool Exists();
        ScriptState Create();
        void Save(ScriptState state);
        ScriptState Load();
        Task SaveAsync(ScriptState state);
    }
}