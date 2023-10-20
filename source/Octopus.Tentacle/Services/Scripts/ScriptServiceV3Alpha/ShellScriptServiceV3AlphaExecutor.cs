using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha
{
    public class ShellScriptServiceV3AlphaExecutor : ScriptServiceV3AlphaExecutor
    {
        readonly IShell shell;
        readonly ISystemLog log;

        public ShellScriptServiceV3AlphaExecutor(
            IScriptWorkspaceFactory workspaceFactory,
            IScriptStateStoreFactory scriptStateStoreFactory,
            IShell shell,
            ISystemLog log)
            : base(workspaceFactory, scriptStateStoreFactory)
        {
            this.shell = shell;
            this.log = log;
        }

        public override bool ValidateExecutionContext(IScriptExecutionContext executionContext)
            => executionContext is LocalShellScriptExecutionContext;

        protected override IRunningScript ExecuteScript(StartScriptCommandV3Alpha startScriptCommand, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            var runningShellScript = new RunningShellScript(shell, workspace, scriptStateStore, workspace.CreateLog(), startScriptCommand.TaskId, cancellationToken, log);

            var thread = new Thread(runningShellScript.Execute) { Name = "Executing PowerShell runningShellScript for " + startScriptCommand.ScriptTicket.TaskId };
            thread.Start();
            return runningShellScript;
        }
    }
}