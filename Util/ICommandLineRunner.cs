using System;
using System.Collections.Generic;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public interface ICommandLineRunner
    {
        bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log);
    }
}