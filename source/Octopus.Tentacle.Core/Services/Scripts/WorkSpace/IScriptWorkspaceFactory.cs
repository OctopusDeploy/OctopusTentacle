using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptWorkspaceFactory
    {
        IScriptWorkspace GetWorkspace(ScriptTicket ticket);

        Task<IScriptWorkspace> PrepareWorkspace(
            ScriptTicket ticket,
            string scriptBody,
            Dictionary<ScriptType, string> scripts,
            ScriptIsolationLevel isolationLevel,
            TimeSpan scriptMutexAcquireTimeout,
            string? scriptMutexName,
            string[]? scriptArguments,
            List<ScriptFile> files,
            CancellationToken cancellationToken);

        List<IScriptWorkspace> GetUncompletedWorkspaces();
    }
}