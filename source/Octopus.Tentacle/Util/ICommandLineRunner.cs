using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Util
{
    public interface ICommandLineRunner
    {
        Task<bool> ExecuteAsync(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log);

        Task<bool> ExecuteAsync(IEnumerable<CommandLineInvocation> commandLineInvocations,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            Action<Exception, string> exception);

        Task<bool> ExecuteAsync(CommandLineInvocation commandLineInvocation, ILog log);

        Task<bool> ExecuteAsync(CommandLineInvocation invocation,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            Action<Exception, string> exception);
    }
}
