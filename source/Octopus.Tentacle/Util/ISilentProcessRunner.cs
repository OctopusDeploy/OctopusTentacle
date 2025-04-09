using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Util
{
    public interface ISilentProcessRunner
    {
        public int ExecuteCommand(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel = default);

        public int ExecuteCommand(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel = default);
    }

    public class SilentProcessRunnerWrapper : ISilentProcessRunner
    {
        public int ExecuteCommand(string executable, string arguments, string workingDirectory, Action<string> info, Action<string> error, CancellationToken cancel = default)
        {
            return SilentProcessRunnerExtended.ExecuteCommand(executable, arguments, workingDirectory, info, error, cancel);
        }

        public int ExecuteCommand(string executable, string arguments, string workingDirectory, Action<string> debug, Action<string> info, Action<string> error, CancellationToken cancel = default)
        {
            return SilentProcessRunner.ExecuteCommand(executable, arguments, workingDirectory, debug, info, error, cancel: cancel);
        }
    }

    public static class SilentProcessRunnerExtended
    {
        public static CmdResult ExecuteCommand(this CommandLineInvocation invocation)
            => ExecuteCommand(invocation, Environment.CurrentDirectory);

        public static CmdResult ExecuteCommand(this CommandLineInvocation invocation, string workingDirectory)
        {
            if (workingDirectory == null)
                throw new ArgumentNullException(nameof(workingDirectory));

            var arguments = $"{invocation.Arguments} {invocation.SystemArguments ?? string.Empty}";
            var infos = new List<string>();
            var errors = new List<string>();

            var exitCode = ExecuteCommand(
                invocation.Executable,
                arguments,
                workingDirectory,
                infos.Add,
                errors.Add
            );

            return new CmdResult(exitCode, infos, errors);
        }

        public static int ExecuteCommand(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel = default)
            => SilentProcessRunner.ExecuteCommand(executable,
                arguments,
                workingDirectory,
                LogFileOnlyLogger.Current.Info,
                info,
                error,
                customEnvironmentVariables: null,
                cancel: cancel);
    }
}