using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Util
{
    public class CommandLineRunner : ICommandLineRunner
    {
        public Task<bool> ExecuteAsync(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log)
        {
            return ExecuteAsync(commandLineInvocations,
                log.Verbose,
                log.Info,
                log.Error,
                log.Error);
        }

        public async Task<bool> ExecuteAsync(IEnumerable<CommandLineInvocation> commandLineInvocations,
                Action<string> debug,
                Action<string> info,
                Action<string> error,
                Action<Exception, string> exception)
        {
            foreach (var invocation in commandLineInvocations)
                if (!await ExecuteAsync(invocation, debug, info, error, exception))
                    return false;

            return true;
        }

        public Task<bool> ExecuteAsync(CommandLineInvocation invocation, ILog log)
            => ExecuteAsync(invocation,
                log.Info,
                log.Info,
                log.Error,
                log.Error);

        public async Task<bool> ExecuteAsync(CommandLineInvocation invocation,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            Action<Exception, string> exception)
        {
            try
            {
                var exitCode = await SilentProcessRunner.ExecuteCommandAsync(
                    invocation.Executable,
                    (invocation.Arguments ?? "") + " " + (invocation.SystemArguments ?? ""),
                    Environment.CurrentDirectory,
                    debug,
                    info,
                    error,
                    cancel: CancellationToken.None,
                    abandon: CancellationToken.None);

                if (exitCode != 0)
                {
                    if (invocation.IgnoreFailedExitCode)
                    {
                        info("The previous command returned a non-zero exit code of: " + exitCode);
                        info("The command that failed was: " + invocation);
                        info("The invocation is set to ignore failed exit codes, continuing...");
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
