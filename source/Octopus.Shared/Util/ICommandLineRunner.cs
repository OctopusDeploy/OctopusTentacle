using System;
using System.Collections.Generic;
using Octopus.Diagnostics;

namespace Octopus.Shared.Util
{
    public interface ICommandLineRunner
    {
        bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ISystemLog log);
        bool Execute(CommandLineInvocation commandLineInvocation, ISystemLog log);

        bool Execute(CommandLineInvocation invocation,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            Action<Exception, string> exception);
    }
}