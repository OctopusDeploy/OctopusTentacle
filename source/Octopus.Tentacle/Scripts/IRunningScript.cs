using System;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IRunningScript
    {
        int ExitCode { get; }
        ProcessState State { get; }
        IScriptLog ScriptLog { get; }
        Task Cancel();
    }
}