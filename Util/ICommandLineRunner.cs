using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.Diagnostics;

namespace Octopus.Shared.Util
{
    public interface ICommandLineRunner
    {
        bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log);
    }
}