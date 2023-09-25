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

        public IRunningScript Execute(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancellationTokenSource, bool runInCurrentThread = false)
        {
            var runningScript = new RunningShellScript(shell, workspace, workspace.CreateLog(), serverTaskId, cancellationTokenSource.Token, log);

            if (!runInCurrentThread)
            {
                var thread = new Thread(runningScript.Execute) { Name = "Executing PowerShell script for " + ticket.TaskId };
                thread.Start();
            }
            else
            {
                runningScript.Execute();
            }

            return runningScript;
        }
    }
}