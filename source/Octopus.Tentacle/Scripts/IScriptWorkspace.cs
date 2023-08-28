using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptWorkspace
    {
        string WorkingDirectory { get; }
        string BootstrapScriptFilePath { get; }
        string[]? ScriptArguments { get; set; }
        ScriptIsolationLevel IsolationLevel { get; set; }
        TimeSpan ScriptMutexAcquireTimeout { get; set; }
        string? ScriptMutexName { get; set; }
        void BootstrapScript(string scriptBody);
        string ResolvePath(string fileName);
        void Delete();
        IScriptLog CreateLog();
        string WriteFile(string filename, string contents);
    }
}