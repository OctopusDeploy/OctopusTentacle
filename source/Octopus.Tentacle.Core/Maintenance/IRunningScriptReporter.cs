using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Core.Maintenance
{
    public interface IRunningScriptReporter
    {
        bool IsRunningScript(ScriptTicket ticket);
    }
}