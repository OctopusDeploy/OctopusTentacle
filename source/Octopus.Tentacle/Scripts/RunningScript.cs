using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class RunningScript
    {
        readonly IScriptWorkspace workspace;
        readonly IShell shell;
        readonly string taskId;
        readonly CancellationToken token;
        readonly ILog log;

        public RunningScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            string taskId,
            CancellationToken token,
            ILog log)
        {
            this.shell = shell;
            this.workspace = workspace;
            this.taskId = taskId;
            this.token = token;
            this.log = log;
            ScriptLog = scriptLog;
            State = ProcessState.Pending;
        }

        public ProcessState State { get; private set; }
        public int ExitCode { get; private set; }

        public IScriptLog ScriptLog { get; }

        public void Execute()
        {
            try
            {
                var shellPath = shell.GetFullPath();

                using (var writer = ScriptLog.CreateWriter())
                {
                    try
                    {
                        using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel,
                            workspace.ScriptMutexAcquireTimeout,
                            workspace.ScriptMutexName ?? nameof(RunningScript),
                            message => writer.WriteOutput(ProcessOutputSource.StdOut, message),
                            taskId,
                            token,
                            log))
                        {
                            RunScript(shellPath, writer);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution canceled.");
                        ExitCode = ScriptExitCodes.CanceledExitCode;
                        State = ProcessState.Complete;
                    }
                    catch (TimeoutException)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution timed out.");
                        ExitCode = ScriptExitCodes.TimeoutExitCode;
                        State = ProcessState.Complete;
                    }
                }
            }
            catch (Exception)
            {
                // Something went really really wrong, probably creating or writing to the log file (Disk space)
                ExitCode = ScriptExitCodes.FatalExitCode;
                State = ProcessState.Complete;
            }
        }

        void RunScript(string shellPath, IScriptLogWriter writer)
        {
            try
            {
                State = ProcessState.Running;

                var exitCode = SilentProcessRunner.ExecuteCommand(
                    shellPath,
                    shell.FormatCommandArguments(workspace.BootstrapScriptFilePath, workspace.ScriptArguments, false),
                    workspace.WorkingDirectory,
                    output => writer.WriteOutput(ProcessOutputSource.Debug, output),
                    output => writer.WriteOutput(ProcessOutputSource.StdOut, output),
                    output => writer.WriteOutput(ProcessOutputSource.StdErr, output),
                    token);

                ExitCode = exitCode;
                State = ProcessState.Complete;
            }
            catch (Exception ex)
            {
                writer.WriteOutput(ProcessOutputSource.StdErr, "An exception was thrown when invoking " + shellPath + ": " + ex.Message);
                writer.WriteOutput(ProcessOutputSource.StdErr, ex.ToString());
                ExitCode = ScriptExitCodes.PowershellInvocationErrorExitCode;
                State = ProcessState.Complete;
            }
        }
    }
}