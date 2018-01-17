using System;
using System.Threading;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class RunningScript
    {
        public const int FatalExitCode = -41;
        public const int PowershellInvocationErrorExitCode = -42;
        public const int CanceledExitCode = -43;
        public const int TimeoutExitCode = -44;

        readonly IScriptWorkspace workspace;
        readonly string taskId;
        readonly CancellationToken token;

        public RunningScript(IScriptWorkspace workspace, IScriptLog log, string taskId, CancellationToken token)
        {
            this.workspace = workspace;
            this.taskId = taskId;
            this.token = token;
            Log = log;
            State = ProcessState.Pending;
        }

        public ProcessState State { get; private set; }
        public int ExitCode { get; private set; }

        public IScriptLog Log { get; }

        public void Execute()
        {
            try
            {
                var powerShellPath = PowerShell.GetFullPath();

                using (var writer = Log.CreateWriter())
                {
                    try
                    {
                        using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel, workspace.ScriptMutexAcquireTimeout, GetType().Name, message => writer.WriteOutput(ProcessOutputSource.StdOut, message), taskId, token))
                            RunScript(powerShellPath, writer);
                    }
                    catch (OperationCanceledException)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution canceled.");
                        ExitCode = CanceledExitCode;
                        State = ProcessState.Complete;
                    }
                    catch (TimeoutException)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution timed out.");
                        ExitCode = TimeoutExitCode;
                        State = ProcessState.Complete;
                    }
                }
            }
            catch (Exception)
            {
                // Something went really really wrong, probably creating or writing to the log file (Disk space)
                ExitCode = FatalExitCode;
                State = ProcessState.Complete;
            }
        }

        private void RunScript(string powerShellPath, IScriptLogWriter writer)
        {
            try
            {
                State = ProcessState.Running;

                var exitCode = SilentProcessRunner.ExecuteCommand(
                    powerShellPath,
                    PowerShell.FormatCommandArguments(workspace.BootstrapScriptFilePath, workspace.ScriptArguments, false),
                    workspace.WorkingDirectory,
                    output => writer.WriteOutput(ProcessOutputSource.Debug, output),
                    output => writer.WriteOutput(ProcessOutputSource.StdOut, output),
                    output => writer.WriteOutput(ProcessOutputSource.StdErr, output),
                    workspace.RunAs,
                    workspace.CustomEnvironmentVariables,
                    token);

                ExitCode = exitCode;
                State = ProcessState.Complete;
            }
            catch (Exception ex)
            {
                writer.WriteOutput(ProcessOutputSource.StdErr, "An exception was thrown when invoking " + powerShellPath + ": " + ex.Message);
                ExitCode = PowershellInvocationErrorExitCode;
                State = ProcessState.Complete;
            }
        }
    }
}