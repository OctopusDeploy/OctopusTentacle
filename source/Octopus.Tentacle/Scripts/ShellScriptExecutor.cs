using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

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

        public IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha startScriptCommand, IScriptWorkspace workspace, ScriptStateStore? scriptStateStore, CancellationToken cancellationToken)
        {
            var runningScript = new RunningShellScript(shell, workspace,  scriptStateStore, workspace.CreateLog(), startScriptCommand.TaskId, cancellationToken, log);

            var thread = new Thread(runningScript.Execute) { Name = "Executing PowerShell script for " + startScriptCommand.ScriptTicket.TaskId };
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