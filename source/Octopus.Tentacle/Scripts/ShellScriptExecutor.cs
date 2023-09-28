using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public class ShellScriptExecutor : IScriptExecutor
    {
        private readonly IShell shell;
        private readonly ISystemLog log;

        public ShellScriptExecutor(IShell shell, ISystemLog log)
        {
            this.shell = shell;
            this.log = log;
        }

        public IRunningScript ExecuteOnBackgroundThread(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, ScriptStateStore? scriptStateStore, CancellationTokenSource cancellationTokenSource)
        {
            var runningScript = new RunningShellScript(shell, workspace,  scriptStateStore, workspace.CreateLog(), serverTaskId, cancellationTokenSource.Token, log);

            var thread = new Thread(runningScript.Execute) { Name = "Executing PowerShell script for " + ticket.TaskId };
            thread.Start();

            return runningScript;
        }

        public IRunningScript Execute(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancellationTokenSource)
        {
            var runningScript = new RunningShellScript(shell, workspace, workspace.CreateLog(), serverTaskId, cancellationTokenSource.Token, log);

            runningScript.Execute();

            return runningScript;
        }
    }
}