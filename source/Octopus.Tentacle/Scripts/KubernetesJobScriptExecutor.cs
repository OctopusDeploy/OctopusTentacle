using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Scripts.Kubernetes;

namespace Octopus.Tentacle.Scripts
{
    public class KubernetesJobScriptExecutor : IScriptExecutor
    {
        private readonly ISystemLog systemLog;
        private readonly IKubernetesJobService jobService;
        private readonly IHomeConfiguration homeConfiguration;

        public KubernetesJobScriptExecutor(ISystemLog systemLog, IKubernetesJobService jobService, IHomeConfiguration homeConfiguration)
        {
            this.systemLog = systemLog;
            this.jobService = jobService;
            this.homeConfiguration = homeConfiguration;
        }

        public IRunningScript ExecuteOnBackgroundThread(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, ScriptStateStore? runningScriptScriptStateStore, CancellationTokenSource cancellationTokenSource)
        {
            var runningScript = new RunningKubernetesJobScript(workspace, workspace.CreateLog(), ticket, serverTaskId, cancellationTokenSource.Token, systemLog, jobService, homeConfiguration);

            var thread = new Thread(runningScript.Execute) { Name = "Executing Kubernetes Job for " + ticket.TaskId };
            thread.Start();

            return runningScript;
        }

        public IRunningScript Execute(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancellationTokenSource)
        {
            var runningScript = new RunningKubernetesJobScript(workspace, workspace.CreateLog(), ticket, serverTaskId, cancellationTokenSource.Token, systemLog, jobService, homeConfiguration);

            runningScript.Execute();

            return runningScript;
        }
    }
}