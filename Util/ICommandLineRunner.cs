using System;
using System.Collections.Generic;
using Octopus.Platform.Diagnostics;

namespace Octopus.Platform.Util
{
    public interface ICommandLineRunner
    {
        bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log);
    }
}