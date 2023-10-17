using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Scripts.Kubernetes;

namespace Octopus.Tentacle.Scripts
{
    public class KubernetesJobScriptExecutor : IScriptExecutor
    {
        private readonly ISystemLog systemLog;
        private readonly IKubernetesJobService jobService;
        readonly IApplicationInstanceSelector appInstanceSelector;

        public KubernetesJobScriptExecutor(ISystemLog systemLog, IKubernetesJobService jobService, IApplicationInstanceSelector appInstanceSelector)
        {
            this.systemLog = systemLog;
            this.jobService = jobService;
            this.appInstanceSelector = appInstanceSelector;
        }

        public IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha startScriptCommand, IScriptWorkspace workspace, ScriptStateStore? scriptStateStore, CancellationToken cancellationToken)
        {
            if (startScriptCommand.ExecutionContext is not KubernetesJobScriptExecutionContext kubernetesJobScriptExecutionContext)
                throw new InvalidOperationException("The ExecutionContext must be of type KubernetesJobScriptExecutionContext");

            var runningScript = new RunningKubernetesJobScript(workspace, workspace.CreateLog(), startScriptCommand.ScriptTicket, startScriptCommand.TaskId, cancellationToken, systemLog, jobService, appInstanceSelector, kubernetesJobScriptExecutionContext);

            var thread = new Thread(runningScript.Execute) { Name = "Executing Kubernetes Job for " + startScriptCommand.ScriptTicket.TaskId };
            thread.Start();

            return runningScript;
        }

        public IRunningScript Execute(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancellationTokenSource)
            => throw new NotSupportedException("We don't support running Kubernetes Jobs on the current thread.");
    }
}