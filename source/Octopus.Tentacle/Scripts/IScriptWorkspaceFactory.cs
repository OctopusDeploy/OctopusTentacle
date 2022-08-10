using System;
using Octopus.Shared.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptWorkspaceFactory
    {
        IScriptWorkspace GetWorkspace(ScriptTicket ticket);
    }
}