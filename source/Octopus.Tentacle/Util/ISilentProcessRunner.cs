using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Util
{
    public interface ISilentProcessRunner
    {
        Task<int> ExecuteCommandAsync(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel = default,
            CancellationToken abandon = default);

        Task<int> ExecuteCommandAsync(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel = default,
            CancellationToken abandon = default);
    }

    public class SilentProcessRunnerWrapper : ISilentProcessRunner
    {
        public Task<int> ExecuteCommandAsync(string executable, string arguments, string workingDirectory, Action<string> info, Action<string> error, CancellationToken cancel = default, CancellationToken abandon = default)
        {
            return SilentProcessRunnerExtended.ExecuteCommandAsync(executable, arguments, workingDirectory, info, error, cancel, abandon);
        }

        public Task<int> ExecuteCommandAsync(string executable, string arguments, string workingDirectory, Action<string> debug, Action<string> info, Action<string> error, CancellationToken cancel = default, CancellationToken abandon = default)
        {
            return SilentProcessRunner.ExecuteCommandAsync(executable, arguments, workingDirectory, debug, info, error, cancel: cancel, abandon: abandon);
        }
    }

    public static class SilentProcessRunnerExtended
    {
        public static async Task<CmdResult> ExecuteCommandAsync(this CommandLineInvocation invocation)
            => await ExecuteCommandAsync(invocation, Environment.CurrentDirectory);

        public static async Task<CmdResult> ExecuteCommandAsync(this CommandLineInvocation invocation, string workingDirectory)
        {
            if (workingDirectory == null)
                throw new ArgumentNullException(nameof(workingDirectory));

            var arguments = $"{invocation.Arguments} {invocation.SystemArguments ?? string.Empty}";
            var infos = new List<string>();
            var errors = new List<string>();

            var exitCode = await ExecuteCommandAsync(
                invocation.Executable,
                arguments,
                workingDirectory,
                infos.Add,
                errors.Add
            );

            return new CmdResult(exitCode, infos, errors);
        }

        public static Task<int> ExecuteCommandAsync(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel = default,
            CancellationToken abandon = default)
            => SilentProcessRunner.ExecuteCommandAsync(executable,
                arguments,
                workingDirectory,
                LogFileOnlyLogger.Current.Info,
                info,
                error,
                customEnvironmentVariables: null,
                cancel: cancel,
                abandon: abandon);
    }
}
