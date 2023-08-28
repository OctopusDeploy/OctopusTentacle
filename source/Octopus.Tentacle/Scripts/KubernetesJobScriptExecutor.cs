using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public class KubernetesJobScriptExecutor : IScriptExecutor
    {
        private readonly IShell shell;
        private readonly ISystemLog systemLog;

        public KubernetesJobScriptExecutor(IShell shell, ISystemLog systemLog)
        {
            this.shell = shell;
            this.systemLog = systemLog;
        }

        public IRunningScript Execute(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancellationTokenSource)
        {
            var runningScript = new RunningKubernetesJobScript(shell, workspace, workspace.CreateLog(), ticket, serverTaskId, cancellationTokenSource.Token, systemLog);

            var thread = new Thread(runningScript.Execute) { Name = "Executing Kubernets Job for " + ticket.TaskId };
            thread.Start();

            return runningScript;
        }
    }
}