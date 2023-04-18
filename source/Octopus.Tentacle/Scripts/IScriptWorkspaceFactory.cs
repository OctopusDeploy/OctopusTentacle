using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptWorkspaceFactory
    {
        IScriptWorkspace GetWorkspace(ScriptTicket ticket);
        IScriptWorkspace PrepareWorkspace(
            ScriptTicket ticket,
            string scriptBody,
            Dictionary<ScriptType, string> scripts,
            ScriptIsolationLevel isolationLevel,
            TimeSpan scriptMutexAcquireTimeout,
            string? scriptMutexName,
            string[]? scriptArguments,
            List<ScriptFile> files);
    }
}