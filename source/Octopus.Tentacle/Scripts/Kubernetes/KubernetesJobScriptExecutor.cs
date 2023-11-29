using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Scripts.Kubernetes
{
    public class KubernetesJobScriptExecutor : IScriptExecutor
    {
        readonly IKubernetesJobService jobService;
        readonly IKubernetesJobContainerResolver containerResolver;
        readonly IApplicationInstanceSelector appInstanceSelector;
        readonly ISystemLog log;

        public KubernetesJobScriptExecutor(IKubernetesJobService jobService, IKubernetesJobContainerResolver containerResolver, IApplicationInstanceSelector appInstanceSelector, ISystemLog log)
        {
            this.jobService = jobService;
            this.containerResolver = containerResolver;
            this.appInstanceSelector = appInstanceSelector;
            this.log = log;
        }

        public bool CanExecute(StartScriptCommandV3Alpha command) => command.ExecutionContext is KubernetesJobScriptExecutionContext;

        public IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, ScriptStateStore scriptStateStore, CancellationToken cancellationToken)
        {
            var runningScript = new RunningKubernetesJobScript(workspace, workspace.CreateLog(), command.ScriptTicket, command.TaskId, cancellationToken, log, scriptStateStore, jobService, containerResolver, appInstanceSelector);

            Task.Run(async () =>
            {
                await runningScript.Execute(cancellationToken);
            }, cancellationToken);

            return runningScript;
        }
    }
}