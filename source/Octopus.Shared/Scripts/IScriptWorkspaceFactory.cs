using System;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Scripts
{
    public interface IScriptWorkspaceFactory
    {
        IScriptWorkspace GetWorkspace(ScriptTicket ticket);
    }
}