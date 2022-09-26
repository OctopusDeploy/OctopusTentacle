using System;
using System.Collections.Generic;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public interface IScriptableViewModel : ICanHaveSensitiveValues
    {
        string InstanceName { get; }
        IEnumerable<CommandLineInvocation> GenerateScript();
        IEnumerable<CommandLineInvocation> GenerateRollbackScript();
    }
}