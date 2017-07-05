using System.Collections.Generic;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public interface IScriptableViewModel
    {
        string InstanceName { get; }
        IEnumerable<CommandLineInvocation> GenerateScript();
        IEnumerable<CommandLineInvocation> GenerateRollbackScript();
    }
}