using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Util
{
    public class CommandLineRunner : ICommandLineRunner
    {
        public bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log)
        {
            return Execute(commandLineInvocations,
                log.Verbose,
                log.Info,
                log.Error,
                log.Error);
        }

        public bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations,
                Action<string> debug,
                Action<string> info,
                Action<string> error,
                Action<Exception, string> exception)
        {
            foreach (var invocation in commandLineInvocations)
                if (!Execute(invocation, debug, info, error, exception))
                    return false;

            return true;
        }

        public bool Execute(CommandLineInvocation invocation, ILog log)
            => Execute(invocation,
                log.Info,
                log.Info,
                log.Error,
                log.Error);

        public bool Execute(CommandLineInvocation invocation,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            Action<Exception, string> exception)
        {
            try
            {
                // Sync boundary: ICommandLineRunner is a public interface consumed by
                // Octopus.Manager.Tentacle (a WPF app) which calls Execute from a
                // ThreadPool.QueueUserWorkItem — no synchronisation context, so
                // GetAwaiter().GetResult() here is deadlock-safe.
                var exitCode = SilentProcessRunner.ExecuteCommandAsync(
                        invocation.Executable,
                        (invocation.Arguments ?? "") + " " + (invocation.SystemArguments ?? ""),
                        Environment.CurrentDirectory,
                        debug,
                        info,
                        error,
                        cancel: CancellationToken.None)
                    .GetAwaiter().GetResult();

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
