using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Services.Scripts.Logging;

namespace Octopus.Tentacle.Core.Services.Scripts
{
    public interface IRunningScript
    {
        int ExitCode { get; }
        ProcessState State { get; }
        IScriptLog ScriptLog { get; }

        Task Cleanup(CancellationToken cancellationToken);
    }
}