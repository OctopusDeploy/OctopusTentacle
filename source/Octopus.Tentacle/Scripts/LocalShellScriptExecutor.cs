using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Scripts
{
    class LocalShellScriptExecutor : IScriptExecutor
    {
        readonly IShell shell;
        readonly ISystemLog log;

        public LocalShellScriptExecutor(IShell shell, ISystemLog log)
        {
            this.shell = shell;
            this.log = log;
        }

        public bool ValidateExecutionContext(IScriptExecutionContext executionContext) => executionContext is LocalShellScriptExecutionContext;

        public IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            var runningScript = new RunningScript(shell, workspace, scriptStateStore, workspace.CreateLog(), command.TaskId, cancellationToken, log);

            var thread = new Thread(runningScript.Execute) { Name = $"Executing {shell.Name} script for " + command.ScriptTicket.TaskId };
            thread.Start();

            return runningScript;
        }
    }
}