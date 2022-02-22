using System;
using System.Collections.Generic;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public interface IScriptableViewModel : ICanHaveSensitiveValues
    {
        string InstanceName { get; }
        IEnumerable<CommandLineInvocation> GenerateScript();
        IEnumerable<CommandLineInvocation> GenerateRollbackScript();
    }
}