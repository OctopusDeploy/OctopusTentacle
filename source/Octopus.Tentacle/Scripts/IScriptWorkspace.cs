using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptWorkspace
    {
        ScriptTicket ScriptTicket { get; }
        string WorkingDirectory { get; }
        string BootstrapScriptFilePath { get; }
        string[]? ScriptArguments { get; set; }
        ScriptIsolationLevel IsolationLevel { get; set; }
        TimeSpan ScriptMutexAcquireTimeout { get; set; }
        string? ScriptMutexName { get; set; }
        void BootstrapScript(string scriptBody, Dictionary<ScriptType, string> otherScripts);
        string ResolvePath(string fileName);
        void Delete();
        Task Delete(CancellationToken cancellationToken);
        IScriptLog CreateLog();
        string LogFilePath { get; }
    }
}