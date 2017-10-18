using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public class CommandLineRunner : ICommandLineRunner
    {
        public bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log)
        {
            foreach (var invocation in commandLineInvocations)
            {
                if (!Execute(invocation, log))
                    return false;
            }

            return true;
        }

        public bool Execute(CommandLineInvocation invocation, ILog log)
        {
            return Execute(invocation, Log.System().Info, log.Info, log.Error, log.Error);
        }

        public bool Execute(CommandLineInvocation invocation, Action<string> metaOutput, Action<string> output, Action<string> error, Action<Exception, string> exception)
        {
            try
            {
                var exitCode = SilentProcessRunner.ExecuteCommand(invocation.Executable, (invocation.Arguments ?? "") + " " + (invocation.SystemArguments ?? ""),
                    Environment.CurrentDirectory,
                    metaOutput,
                    output,
                    error,
                    CancellationToken.None);

                if (exitCode != 0)
                {
                    if (invocation.IgnoreFailedExitCode)
                    {
                        output("The previous command returned a non-zero exit code of: " + exitCode);
                        output("The command that failed was: " + invocation);
                        output("The invocation is set to ignore failed exit codes, continuing...");
                    }
                    else
                    {
                        error("The previous command returned a non-zero exit code of: " + exitCode);
                        error("The command that failed was: " + invocation);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                exception(ex, "Exception calling command: " + invocation);
                return false;
            }
            return true;
        }
    }
}
