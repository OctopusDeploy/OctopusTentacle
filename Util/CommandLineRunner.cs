using System;
using System.Collections.Generic;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public class CommandLineRunner : ICommandLineRunner
    {
        public bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log)
        {
            foreach (var invocation in commandLineInvocations)
            {
                try
                {
                    var exitCode = SilentProcessRunner.ExecuteCommand(invocation.Executable, (invocation.Arguments ?? "") + " " + (invocation.SystemArguments ?? ""),
                        Environment.CurrentDirectory,
                        log.Info,
                        log.Error);

                    if (exitCode != 0)
                    {
                        if (invocation.IgnoreFailedExitCode)
                        {
                            log.Info("The previous command returned a non-zero exit code of: " + exitCode);
                            log.Info("The command that failed was: " + invocation);
                            log.Info("The invocation is set to ignore failed exit codes, continuing...");
                        }
                        else
                        {
                            log.Error("The previous command returned a non-zero exit code of: " + exitCode);
                            log.Error("The command that failed was: " + invocation);
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    log.Error("The command that caused the exception was: " + invocation);
                    return false;
                }
            }

            return true;
        }
    }
}