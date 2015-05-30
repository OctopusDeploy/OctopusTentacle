using System;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Scripts
{
    public interface IScriptWorkspace
    {
        string WorkingDirectory { get; }
        string BootstrapScriptFilePath { get; }
        ScriptIsolationLevel IsolationLevel { get; set; }
        void BootstrapScript(string scriptBody);
        string ResolvePath(string fileName);
        void Delete();
    }
}