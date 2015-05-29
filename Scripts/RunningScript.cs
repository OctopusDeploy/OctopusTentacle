using System;
using System.Threading;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class RunningScript
    {
        readonly CancellationTokenSource cancel = new CancellationTokenSource();
        readonly IScriptWorkspace workspace;
        readonly IScriptLog log;

        public RunningScript(IScriptWorkspace workspace, IScriptLog log)
        {
            this.workspace = workspace;
            this.log = log;
            State = ProcessState.Pending;
        }

        public ProcessState State { get; private set; }
        public int ExitCode { get; private set; }
        public IScriptLog Log { get { return log; } }

        public void Execute()
        {
            var powerShellPath = PowerShell.GetFullPath();
            
            using (var writer = log.CreateWriter())
            using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel, message => writer.WriteOutput(ProcessOutputSource.StdOut, message)))
            {
                try
                {
                    State = ProcessState.Running;

                    var exitCode = SilentProcessRunner.ExecuteCommand(powerShellPath, PowerShell.FormatCommandArguments(workspace.BootstrapScriptFilePath, false), workspace.WorkingDirectory,
                        output => writer.WriteOutput(ProcessOutputSource.StdOut, output),
                        output => writer.WriteOutput(ProcessOutputSource.StdErr, output),
                        cancel.Token);

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

        public void Cancel()
        {
            cancel.Cancel();
        }
    }
}