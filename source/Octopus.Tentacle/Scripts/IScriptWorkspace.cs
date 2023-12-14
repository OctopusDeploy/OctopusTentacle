using System;
using System.IO;
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
        void BootstrapScript(string scriptBody);
        string ResolvePath(string fileName);
        Task Delete(CancellationToken cancellationToken);
        IScriptLog CreateLog();
        string LogFilePath { get; }
        void WriteFile(string filename, string contents);
        Stream OpenFileStreamForReading(string filename);
    }
}