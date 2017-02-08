using System;
using System.Threading;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class RunningScript
    {
        public const int CancelledExitCode = -1337;

        readonly IScriptWorkspace workspace;
        readonly IScriptLog log;
        readonly CancellationToken token;

        public RunningScript(IScriptWorkspace workspace, IScriptLog log, CancellationToken token)
        {
            this.workspace = workspace;
            this.log = log;
            this.token = token;
            State = ProcessState.Pending;
        }

        public ProcessState State { get; private set; }
        public int ExitCode { get; private set; }

        public IScriptLog Log
        {
            get { return log; }
        }

        public void Execute()
        {
            var powerShellPath = PowerShell.GetFullPath();

            using (var writer = log.CreateWriter())
            {
                try
                {
                    using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel, workspace.ScriptMutexAcquireTimeout, GetType().Name, message => writer.WriteOutput(ProcessOutputSource.StdOut, message), token))
                    {
                        try
                        {
                            State = ProcessState.Running;

                            var exitCode = SilentProcessRunner.ExecuteCommand(powerShellPath, PowerShell.FormatCommandArguments(workspace.BootstrapScriptFilePath, workspace.ScriptArguments, false), workspace.WorkingDirectory,
                                output => writer.WriteOutput(ProcessOutputSource.StdOut, output),
                                output => writer.WriteOutput(ProcessOutputSource.StdErr, output),
                                token);

                            ExitCode = exitCode;
                            State = ProcessState.Complete;
                        }
                        catch (Exception ex)
                        {
                            writer.WriteOutput(ProcessOutputSource.StdErr, "An exception was thrown when invoking " + powerShellPath + ": " + ex.Message);
                            ExitCode = -42;
                            State = ProcessState.Complete;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution canceled.");
                    ExitCode = CancelledExitCode;
                    State = ProcessState.Complete;
                }
            }
        }
    }
}