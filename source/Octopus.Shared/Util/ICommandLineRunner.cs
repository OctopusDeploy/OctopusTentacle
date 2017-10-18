using System;
using System.Collections.Generic;
using Octopus.Diagnostics;

namespace Octopus.Shared.Util
{
    public interface ICommandLineRunner
    {
        bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log);
        bool Execute(CommandLineInvocation commandLineInvocation, ILog log);
        bool Execute(CommandLineInvocation invocation, Action<string> metaOutput, Action<string> output, Action<string> error, Action<Exception, string> exception);
    }
}
