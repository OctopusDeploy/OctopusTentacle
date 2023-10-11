using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Scripts
{
    public class ShellScriptExecutor : IScriptExecutor
    {
        readonly IShell shell;
        readonly ISystemLog log;
        readonly string shellName;

        public ShellScriptExecutor(IShell shell, ISystemLog log)
        {
            this.shell = shell;
            shellName = shell.GetType().Name;
            this.log = log;
        }

        public IRunningScript ExecuteOnThread(StartScriptCommandV2 command, IScriptWorkspace workspace, ScriptStateStore? scriptStateStore, CancellationTokenSource cancellationTokenSource)
        {
            var runningScript = new RunningShellScript(shell, workspace,  scriptStateStore, workspace.CreateLog(), command.TaskId, cancellationTokenSource.Token, log);

            var thread = new Thread(runningScript.Execute) { Name = $"Executing {shellName} script for " + command.ScriptTicket.TaskId };
            thread.Start();

            return runningScript;
        }
    }
}