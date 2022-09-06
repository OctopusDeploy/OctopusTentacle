using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptWorkspaceFactory
    {
        IScriptWorkspace GetWorkspace(ScriptTicket ticket);
    }
}